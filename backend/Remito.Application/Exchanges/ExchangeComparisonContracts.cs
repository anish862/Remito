namespace Remito.Application.Exchanges;

/// <summary>A real exchange quote for a requested currency corridor and amount.</summary>
public sealed record ExchangeOffer(
    Guid Id,
    string Name,
    string? Logo,
    decimal Rate,
    decimal? Fee,
    decimal? AmountReceived,
    string FeeDisclosure,
    DateTimeOffset LastUpdated,
    bool IsStale,
    bool HasApp,
    bool IsSponsored,
    bool IsBestRate);

/// <summary>Explicitly labelled sponsored offers shown separately from organic ranking.</summary>
public sealed record SponsoredExchangeSlot(string Label, IReadOnlyList<ExchangeOffer> Exchanges);

/// <summary>Exchange offers and comparison disclosure for one corridor and amount.</summary>
public sealed record ExchangeComparisonResponse(
    string From,
    string To,
    decimal Amount,
    IReadOnlyList<ExchangeOffer> Exchanges,
    SponsoredExchangeSlot? SponsoredSlot,
    string Disclaimer,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>Options for filtering, ordering, and paging exchange offers.</summary>
public sealed record ExchangeComparisonOptions(
    string? Sort,
    bool? HasApp,
    string? Search,
    int Page,
    int PageSize);

/// <summary>Reads exchange offers for a corridor and amount.</summary>
public interface IExchangeComparisonService
{
    Task<ExchangeComparisonResponse> GetExchangesAsync(
        string from,
        string to,
        decimal amount,
        ExchangeComparisonOptions options,
        CancellationToken cancellationToken);
}

/// <summary>Represents an invalid comparison request or unavailable corridor.</summary>
public sealed class ExchangeComparisonException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
