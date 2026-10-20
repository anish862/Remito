using Microsoft.EntityFrameworkCore;
using Remito.Domain.Entities;

namespace Remito.Infrastructure.Persistence;

public sealed class RemitoDbContext(DbContextOptions<RemitoDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<Corridor> Corridors => Set<Corridor>();
    public DbSet<RateSource> RateSources => Set<RateSource>();
    public DbSet<MarketRate> MarketRates => Set<MarketRate>();
    public DbSet<RateHistory> RateHistories => Set<RateHistory>();
    public DbSet<Exchange> Exchanges => Set<Exchange>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<ExchangeFee> ExchangeFees => Set<ExchangeFee>();
    public DbSet<Favourite> Favourites => Set<Favourite>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<RedirectEvent> RedirectEvents => Set<RedirectEvent>();
    public DbSet<ReferralConversion> ReferralConversions => Set<ReferralConversion>();
    public DbSet<CommissionLedger> CommissionLedgers => Set<CommissionLedger>();
    public DbSet<AdCampaign> AdCampaigns => Set<AdCampaign>();
    public DbSet<AdCreative> AdCreatives => Set<AdCreative>();
    public DbSet<AdImpression> AdImpressions => Set<AdImpression>();
    public DbSet<NewsItem> NewsItems => Set<NewsItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RemitoDbContext).Assembly);
    }
}
