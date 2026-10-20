namespace Remito.Domain.Entities;

public sealed class Currency
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Symbol { get; set; }
    public int DecimalPlaces { get; set; }
}

public sealed class Corridor
{
    public Guid Id { get; set; }
    public required string FromCurrency { get; set; }
    public required string ToCurrency { get; set; }
    public bool IsActive { get; set; }
    public Currency FromCurrencyEntity { get; set; } = null!;
    public Currency ToCurrencyEntity { get; set; } = null!;
}
