using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.DependencyInjection;
using Remito.Application.Engagement;
using Remito.Infrastructure;
using Remito.Infrastructure.Rates;
using Remito.Infrastructure.Engagement;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplicationInsightsTelemetryWorkerService();

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
    builder.Services.AddHangfire(configuration => configuration.UseSqlServerStorage(connectionString));
    builder.Services.AddHangfireServer();
}

var host = builder.Build();
if (!string.IsNullOrWhiteSpace(connectionString))
{
    var recurringJobs = host.Services.GetRequiredService<IRecurringJobManager>();
    recurringJobs.AddOrUpdate(
        "rates-ingestion",
        Job.FromExpression<RateIngestionJob>(job => job.RunAsync(CancellationToken.None)),
        Cron.Minutely(),
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    recurringJobs.AddOrUpdate(
        "rates-history-rollup",
        Job.FromExpression<RateHistoryRollupJob>(job => job.RunAsync(CancellationToken.None)),
        Cron.Hourly(),
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    recurringJobs.AddOrUpdate(
        "rate-alert-evaluation",
        Job.FromExpression<RateAlertEvaluationJob>(job => job.RunAsync(CancellationToken.None)),
        Cron.Minutely(),
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    recurringJobs.AddOrUpdate(
        "rate-alert-daily-summary",
        Job.FromExpression<RateAlertEvaluationJob>(job => job.RunDailySummaryAsync(CancellationToken.None)),
        Cron.Daily(8),
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    if (!string.IsNullOrWhiteSpace(builder.Configuration["PartnerReconciliation:ImportDirectory"]))
    {
        recurringJobs.AddOrUpdate(
            "partner-reconciliation-import",
            Job.FromExpression<PartnerReconciliationJob>(job => job.RunAsync(CancellationToken.None)),
            Cron.Daily(),
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }

    if (!string.IsNullOrWhiteSpace(builder.Configuration["News:Rss:FeedUrl"]))
    {
        recurringJobs.AddOrUpdate(
            "news-rss-import",
            Job.FromExpression<IRssNewsImportJob>(job => job.RunAsync(CancellationToken.None)),
            Cron.Daily(7),
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}

host.Run();
