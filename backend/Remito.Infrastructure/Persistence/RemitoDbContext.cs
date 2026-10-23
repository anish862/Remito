using Microsoft.EntityFrameworkCore;
using Remito.Domain.Entities;

namespace Remito.Infrastructure.Persistence;

public sealed class RemitoDbContext(DbContextOptions<RemitoDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<UserRoleAssignment> UserRoles => Set<UserRoleAssignment>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserConsent> UserConsents => Set<UserConsent>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<Corridor> Corridors => Set<Corridor>();
    public DbSet<RateSource> RateSources => Set<RateSource>();
    public DbSet<MarketRate> MarketRates => Set<MarketRate>();
    public DbSet<RateHistory> RateHistories => Set<RateHistory>();
    public DbSet<RateQuarantine> RateQuarantines => Set<RateQuarantine>();
    public DbSet<ManualRateOverride> ManualRateOverrides => Set<ManualRateOverride>();
    public DbSet<Exchange> Exchanges => Set<Exchange>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<ExchangeFee> ExchangeFees => Set<ExchangeFee>();
    public DbSet<Favourite> Favourites => Set<Favourite>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<RedirectEvent> RedirectEvents => Set<RedirectEvent>();
    public DbSet<ReferralConversion> ReferralConversions => Set<ReferralConversion>();
    public DbSet<CommissionLedger> CommissionLedgers => Set<CommissionLedger>();
    public DbSet<PartnerReconciliationIssue> PartnerReconciliationIssues => Set<PartnerReconciliationIssue>();
    public DbSet<PartnerReportRow> PartnerReportRows => Set<PartnerReportRow>();
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
