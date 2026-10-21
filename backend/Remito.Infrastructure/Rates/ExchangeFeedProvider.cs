using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Remito.Domain.Entities;

namespace Remito.Infrastructure.Rates;

public sealed class ExchangeFeedProvider(
    IHttpClientFactory httpClientFactory,
    ExchangeFeedDefinition definition,
    TimeProvider timeProvider) : IRateProvider
{
    public Guid SourceId => definition.SourceId;
    public string SourceName => definition.Name;
    public RateSourceType SourceType => RateSourceType.Exchange;
    public TimeSpan StalenessThreshold => TimeSpan.FromMinutes(definition.StalenessMinutes);

    public async Task<IReadOnlyList<RateQuote>> FetchAsync(CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(definition.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"The configured feed for {SourceName} must use HTTPS.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        foreach (var header in definition.Headers)
        {
            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                throw new InvalidOperationException($"The configured HTTP header '{header.Key}' is invalid.");
            }
        }

        using var response = await httpClientFactory.CreateClient(nameof(ExchangeFeedProvider))
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = FeedParser.Parse(definition.Strategy, payload);
        var fetchedAt = timeProvider.GetUtcNow();
        return parsed.Select(row => new RateQuote(
                RateNormalizer.NormalizePair(row.Pair),
                row.Bid,
                row.Ask,
                row.Mid ?? (row.Bid + row.Ask) / 2m,
                SourceId,
                fetchedAt))
            .ToArray();
    }
}

internal sealed record ParsedRate(string Pair, decimal Bid, decimal Ask, decimal? Mid);

internal static class RateJsonParser
{
    public static IReadOnlyList<ParsedRate> ReadRows(JsonElement root)
    {
        var rows = root.ValueKind switch
        {
            JsonValueKind.Array => root,
            JsonValueKind.Object when root.TryGetProperty("quotes", out var quotes) => quotes,
            JsonValueKind.Object when root.TryGetProperty("rates", out var rates) => rates,
            _ => throw new JsonException("Rate JSON must be an array or contain a quotes/rates array.")
        };

        if (rows.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The rate JSON quotes/rates property must be an array.");
        }

        return rows.EnumerateArray().Select(row =>
        {
            var pair = ReadString(row, "pair");
            var bid = ReadDecimal(row, "bid");
            var ask = ReadDecimal(row, "ask");
            var mid = row.TryGetProperty("mid", out var midElement)
                && midElement.ValueKind != JsonValueKind.Null
                ? midElement.GetDecimal()
                : (decimal?)null;
            return new ParsedRate(pair, bid, ask, mid);
        }).ToArray();
    }

    private static string ReadString(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new JsonException($"Rate JSON is missing a string '{name}' value.");

    private static decimal ReadDecimal(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.TryGetDecimal(out var number)
            ? number
            : throw new JsonException($"Rate JSON is missing a numeric '{name}' value.");
}

internal static partial class FeedParser
{
    private static readonly Regex HtmlRowRegex = new(
        "<tr\\b[^>]*>(.*?)</tr\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex HtmlCellRegex = new(
        "<t[dh]\\b[^>]*>(.*?)</t[dh]\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex HtmlTagRegex = new(
        "<[^>]+>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public static IReadOnlyList<ParsedRate> Parse(string strategy, string payload) =>
        strategy.Trim().ToLowerInvariant() switch
        {
            "json" => ParseJson(payload),
            "csv" => ParseCsv(payload),
            "html" => ParseHtml(payload),
            _ => throw new InvalidOperationException($"Unsupported exchange feed strategy '{strategy}'.")
        };

    private static IReadOnlyList<ParsedRate> ParseJson(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return RateJsonParser.ReadRows(document.RootElement);
    }

    private static IReadOnlyList<ParsedRate> ParseCsv(string payload)
    {
        var lines = payload.Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2)
        {
            throw new FormatException("The CSV rate feed must include a header and at least one rate.");
        }

        var headers = SplitCsvLine(lines[0]).Select(header => header.Trim().ToLowerInvariant()).ToArray();
        var pairColumn = FindColumn(headers, "pair");
        var bidColumn = FindColumn(headers, "bid");
        var askColumn = FindColumn(headers, "ask");
        var midColumn = Array.IndexOf(headers, "mid");
        return lines.Skip(1).Select(line =>
        {
            var values = SplitCsvLine(line);
            var pair = ReadColumn(values, pairColumn, "pair");
            var bid = ParseDecimal(ReadColumn(values, bidColumn, "bid"));
            var ask = ParseDecimal(ReadColumn(values, askColumn, "ask"));
            var mid = midColumn >= 0 && midColumn < values.Length && !string.IsNullOrWhiteSpace(values[midColumn])
                ? ParseDecimal(values[midColumn])
                : (decimal?)null;
            return new ParsedRate(pair, bid, ask, mid);
        }).ToArray();
    }

    private static IReadOnlyList<ParsedRate> ParseHtml(string payload)
    {
        var rows = HtmlRowRegex.Matches(payload);
        if (rows.Count < 2)
        {
            throw new FormatException("The HTML feed must contain a header row and at least one rate row.");
        }

        var headerCells = HtmlCellRegex.Matches(rows[0].Groups[1].Value)
            .Select(cell => DecodeHtmlCell(cell.Groups[1].Value).ToLowerInvariant())
            .ToArray();
        var pairColumn = FindColumn(headerCells, "pair");
        var bidColumn = FindColumn(headerCells, "bid");
        var askColumn = FindColumn(headerCells, "ask");
        var midColumn = Array.IndexOf(headerCells, "mid");
        return rows.Skip(1).Select(row =>
        {
            var cells = HtmlCellRegex.Matches(row.Groups[1].Value)
                .Select(cell => DecodeHtmlCell(cell.Groups[1].Value))
                .ToArray();
            var pair = ReadColumn(cells, pairColumn, "pair");
            var bid = ParseDecimal(ReadColumn(cells, bidColumn, "bid"));
            var ask = ParseDecimal(ReadColumn(cells, askColumn, "ask"));
            var mid = midColumn >= 0 && midColumn < cells.Length && !string.IsNullOrWhiteSpace(cells[midColumn])
                ? ParseDecimal(cells[midColumn])
                : (decimal?)null;
            return new ParsedRate(pair, bid, ask, mid);
        }).ToArray();
    }

    private static string[] SplitCsvLine(string line) =>
        Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)")
            .Select(value => value.Trim().Trim('"'))
            .ToArray();

    private static int FindColumn(string[] headers, string name)
    {
        var index = Array.IndexOf(headers, name);
        return index >= 0 ? index : throw new FormatException($"The rate feed is missing the '{name}' column.");
    }

    private static string ReadColumn(string[] values, int index, string name) =>
        index < values.Length && !string.IsNullOrWhiteSpace(values[index])
            ? values[index]
            : throw new FormatException($"A rate feed row is missing the '{name}' value.");

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new FormatException($"The rate value '{value}' is not a valid decimal number.");

    private static string DecodeHtmlCell(string value) =>
        WebUtility.HtmlDecode(HtmlTagRegex.Replace(value, string.Empty)).Trim();
}
