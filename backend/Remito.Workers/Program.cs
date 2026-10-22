using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.DependencyInjection;
using Remito.Infrastructure;
using Remito.Infrastructure.Rates;

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
}

host.Run();
