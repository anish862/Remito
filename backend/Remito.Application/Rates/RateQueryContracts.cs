namespace Remito.Application.Rates;

/// <summary>Latest available value and freshness for a quote currency.</summary>
public sealed record LatestRateItem(
    string Currency,
    decimal Rate,
    decimal Change24HoursPercent,
    DateTimeOffset LastUpdated,
    bool IsStale);

/// <summary>Latest rates for the requested base currency.</summary>
public sealed record LatestRatesResponse(string Base, IReadOnlyList<LatestRateItem> Rates);

/// <summary>A timestamped point in a currency pair's rate history.</summary>
public sealed record RateHistoryPoint(DateTimeOffset Timestamp, decimal Rate);

/// <summary>Downsampled rate history and summary statistics for a currency pair.</summary>
public sealed record RateHistoryResponse(
    string Pair,
    string Range,
    IReadOnlyList<RateHistoryPoint> Points,
    decimal Minimum,
    decimal Maximum,
    decimal Open,
    decimal Close);

/// <summary>Converted amount with the rate and freshness used to calculate it.</summary>
public sealed record CurrencyConversionResponse(
    decimal Amount,
    string From,
    string To,
    decimal Result,
    decimal Rate,
    DateTimeOffset Timestamp,
    bool IsStale);

public interface IRateQueryService
{
    Task<LatestRatesResponse> GetLatestAsync(
        string baseCurrency,
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken);

    Task<RateHistoryResponse> GetHistoryAsync(
        string pair,
        string range,
        CancellationToken cancellationToken);

    Task<CurrencyConversionResponse> ConvertAsync(
        decimal amount,
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken);
}

public sealed class RateQueryException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
