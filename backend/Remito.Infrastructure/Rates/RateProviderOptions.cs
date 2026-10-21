namespace Remito.Infrastructure.Rates;

public sealed class MarketRateProviderOptions
{
    public string? Endpoint { get; init; }
    public Guid SourceId { get; init; }
    public string SourceName { get; init; } = "Market FX";
    public string? ApiKey { get; set; }
    public string ApiKeyHeader { get; init; } = "Authorization";
    public string ApiKeyPrefix { get; init; } = "Bearer ";
    public int StalenessMinutes { get; init; } = 15;
}

public sealed class ExchangeFeedOptions
{
    public List<ExchangeFeedDefinition> Feeds { get; init; } = [];
}

public sealed class ExchangeFeedDefinition
{
    public required string Name { get; init; }
    public required string Endpoint { get; init; }
    public Guid SourceId { get; init; }
    public string Strategy { get; init; } = "json";
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public int StalenessMinutes { get; init; } = 60;
}

public sealed class RateGuardOptions
{
    public decimal MaximumVariancePercent { get; init; } = 10m;
    public int HistoryRetentionDays { get; init; } = 400;
    public Dictionary<string, PairSanityBounds> PairBounds { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PairSanityBounds
{
    public decimal Minimum { get; init; }
    public decimal Maximum { get; init; }
}

public sealed class ManualOverrideOptions
{
    public Guid SourceId { get; init; } = Guid.Parse("f6c3e61a-d1e1-47dc-882f-dc818e641000");
    public string SourceName { get; init; } = "Manual override";
    public int StalenessMinutes { get; init; } = 60;
}

public sealed class FixtureRateProviderOptions
{
    public bool Enabled { get; init; }
    public Guid SourceId { get; init; } = Guid.Parse("f6c3e61a-d1e1-47dc-882f-dc818e641001");
    public string SourceName { get; init; } = "Local fixture";
    public string Path { get; init; } = "Fixtures/rates.json";
    public int StalenessMinutes { get; init; } = 15;
}
