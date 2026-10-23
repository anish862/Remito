using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Remito.Application.Engagement;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Infrastructure.Engagement;

public sealed class PartnerReconciliationJob(
    RemitoDbContext dbContext,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<PartnerReconciliationJob> logger) : IPartnerReconciliationJob
{
    private const int MaximumFileBytes = 10 * 1024 * 1024;
    private static readonly string[] RequiredHeaders =
    [
        "external_ref", "click_id", "converted_at", "amount", "commission", "currency"
    ];

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var configuredDirectory = configuration["PartnerReconciliation:ImportDirectory"];
        if (string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return;
        }

        var directory = Path.GetFullPath(configuredDirectory);
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"Partner reconciliation import directory does not exist: '{directory}'.");
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.csv", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileInfo = new FileInfo(file);
            if (fileInfo.Length > MaximumFileBytes)
            {
                throw new InvalidDataException($"Partner report '{Path.GetFileName(file)}' exceeds 10 MB.");
            }

            var exchangeId = ParseExchangeId(Path.GetFileName(file));
            var rows = await File.ReadAllLinesAsync(file, cancellationToken);
            await ImportFileAsync(exchangeId, rows, cancellationToken);
            logger.LogInformation(
                "Partner reconciliation report imported. ExchangeId={ExchangeId} RowCount={RowCount}",
                exchangeId,
                Math.Max(0, rows.Length - 1));
        }

        await FlagMissingReportRowsAsync(cancellationToken);
        await FlagMissingPostbacksAsync(cancellationToken);
    }

    private async Task ImportFileAsync(Guid exchangeId, IReadOnlyList<string> lines, CancellationToken cancellationToken)
    {
        if (!await dbContext.Exchanges.AnyAsync(exchange => exchange.Id == exchangeId, cancellationToken))
        {
            throw new InvalidDataException($"Partner report refers to unknown exchange '{exchangeId}'.");
        }

        if (lines.Count < 1)
        {
            throw new InvalidDataException($"Partner report for exchange '{exchangeId}' is empty.");
        }

        var headers = ParseCsvLine(lines[0])
            .Select((name, index) => (name: name.Trim().ToLowerInvariant(), index))
            .ToDictionary(item => item.name, item => item.index, StringComparer.Ordinal);
        if (RequiredHeaders.Any(header => !headers.ContainsKey(header)))
        {
            throw new InvalidDataException(
                $"Partner report for exchange '{exchangeId}' is missing required columns.");
        }

        foreach (var line in lines.Skip(1).Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fields = ParseCsvLine(line);
            string Field(string name) =>
                headers[name] < fields.Count ? fields[headers[name]].Trim() : string.Empty;

            var externalRef = Field("external_ref");
            var clickId = Field("click_id");
            if (string.IsNullOrWhiteSpace(externalRef) || externalRef.Length > 200
                || string.IsNullOrWhiteSpace(clickId) || clickId.Length > 100
                || !DateTimeOffset.TryParse(
                    Field("converted_at"),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var convertedAt)
                || !decimal.TryParse(Field("amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
                || amount <= 0
                || !decimal.TryParse(
                    Field("commission"),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var commission)
                || commission < 0)
            {
                throw new InvalidDataException($"Partner report for exchange '{exchangeId}' contains an invalid row.");
            }

            var currency = Field("currency").ToUpperInvariant();
            if (currency.Length != 3
                || !await dbContext.Currencies.AnyAsync(item => item.Code == currency, cancellationToken))
            {
                throw new InvalidDataException($"Partner report for exchange '{exchangeId}' contains an invalid currency.");
            }

            var stored = await dbContext.PartnerReportRows.SingleOrDefaultAsync(
                row => row.ExchangeId == exchangeId && row.ExternalRef == externalRef,
                cancellationToken);
            if (stored is null)
            {
                dbContext.PartnerReportRows.Add(new PartnerReportRow
                {
                    Id = Guid.NewGuid(),
                    ExchangeId = exchangeId,
                    ExternalRef = externalRef,
                    ClickId = clickId,
                    ConvertedAt = convertedAt,
                    Amount = amount,
                    Commission = commission,
                    CurrencyCode = currency
                });
            }
            else
            {
                if (stored.ClickId != clickId || stored.Amount != amount
                    || stored.Commission != commission || stored.CurrencyCode != currency)
                {
                    throw new InvalidDataException(
                        $"Partner report external reference '{externalRef}' conflicts with an imported row.");
                }

                stored.ConvertedAt = convertedAt;
            }

            var redirectExists = await dbContext.RedirectEvents.AnyAsync(
                item => item.ExchangeId == exchangeId && item.ClickId == clickId,
                cancellationToken);
            var conversion = await dbContext.ReferralConversions.SingleOrDefaultAsync(
                item => item.ExchangeId == exchangeId && item.ExternalRef == externalRef,
                cancellationToken);
            var conversionExists = conversion is not null
                && conversion.ClickId == clickId
                && conversion.Amount == amount;
            var issueType = !redirectExists
                ? "UnmatchedReport"
                : !conversionExists
                    ? "MissingPostback"
                    : null;
            await UpsertIssueAsync(exchangeId, externalRef, clickId, issueType, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task FlagMissingReportRowsAsync(CancellationToken cancellationToken)
    {
        var conversions = await dbContext.ReferralConversions.AsNoTracking()
            .Where(conversion => conversion.Status == ConversionStatus.Confirmed
                && conversion.ExternalRef != null)
            .Select(conversion => new { conversion.ExchangeId, conversion.ExternalRef, conversion.ClickId })
            .ToListAsync(cancellationToken);
        foreach (var conversion in conversions)
        {
            if (await dbContext.PartnerReportRows.AnyAsync(row => row.ExchangeId == conversion.ExchangeId
                    && row.ExternalRef == conversion.ExternalRef, cancellationToken))
            {
                continue;
            }

            await UpsertIssueAsync(
                conversion.ExchangeId,
                conversion.ExternalRef!,
                conversion.ClickId,
                "MissingReportRow",
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task FlagMissingPostbacksAsync(CancellationToken cancellationToken)
    {
        var graceHours = configuration.GetValue<int?>("PartnerReconciliation:PostbackGraceHours") ?? 48;
        if (graceHours is < 1 or > 720)
        {
            throw new InvalidOperationException("Partner postback grace period must be between 1 and 720 hours.");
        }

        var cutoff = timeProvider.GetUtcNow().AddHours(-graceHours);
        var query = dbContext.RedirectEvents.AsNoTracking()
            .Where(redirect => redirect.CreatedAt < cutoff);
        List<RedirectEvent> redirects;
        if (dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var candidates = (await dbContext.RedirectEvents.AsNoTracking().ToListAsync(cancellationToken))
                .Where(item => item.CreatedAt < cutoff)
                .ToList();
            var clickIds = candidates.Select(item => item.ClickId).ToArray();
            var convertedIds = await dbContext.ReferralConversions.AsNoTracking()
                .Where(conversion => clickIds.Contains(conversion.ClickId))
                .Select(conversion => conversion.ClickId)
                .ToListAsync(cancellationToken);
            redirects = candidates.Where(item => !convertedIds.Contains(item.ClickId, StringComparer.Ordinal))
                .ToList();
        }
        else
        {
            redirects = await query
            .Where(redirect => redirect.CreatedAt < cutoff
                && !dbContext.ReferralConversions.Any(conversion => conversion.ClickId == redirect.ClickId))
            .ToListAsync(cancellationToken);
        }
        foreach (var redirect in redirects)
        {
            await UpsertIssueAsync(
                redirect.ExchangeId,
                redirect.ClickId,
                redirect.ClickId,
                "MissingPostback",
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertIssueAsync(
        Guid exchangeId,
        string externalRef,
        string clickId,
        string? issueType,
        CancellationToken cancellationToken)
    {
        var issue = await dbContext.PartnerReconciliationIssues.SingleOrDefaultAsync(
            item => item.ExchangeId == exchangeId && item.ExternalRef == externalRef,
            cancellationToken);
        if (issueType is null)
        {
            if (issue is not null)
            {
                issue.IsResolved = true;
            }

            return;
        }

        if (issue is null)
        {
            dbContext.PartnerReconciliationIssues.Add(new PartnerReconciliationIssue
            {
                Id = Guid.NewGuid(),
                ExchangeId = exchangeId,
                ExternalRef = externalRef,
                ClickId = clickId,
                IssueType = issueType,
                CreatedAt = timeProvider.GetUtcNow(),
                IsResolved = false
            });
        }
        else
        {
            issue.ClickId = clickId;
            issue.IssueType = issueType;
            issue.IsResolved = false;
        }
    }

    private static Guid ParseExchangeId(string fileName)
    {
        var separator = fileName.IndexOf('_');
        var idPart = separator < 0 ? Path.GetFileNameWithoutExtension(fileName) : fileName[..separator];
        if (!Guid.TryParse(idPart, out var exchangeId))
        {
            throw new InvalidDataException(
                $"Partner report filename '{fileName}' must begin with an exchange GUID.");
        }

        return exchangeId;
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var value = new System.Text.StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (quoted && character == '"' && index + 1 < line.Length && line[index + 1] == '"')
            {
                value.Append('"');
                index++;
            }
            else if (character == '"')
            {
                quoted = !quoted;
            }
            else if (character == ',' && !quoted)
            {
                fields.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(character);
            }
        }

        if (quoted)
        {
            throw new InvalidDataException("Partner CSV contains an unterminated quoted field.");
        }

        fields.Add(value.ToString());
        return fields;
    }
}
