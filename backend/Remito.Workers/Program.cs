using Hangfire;
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
    RecurringJob.AddOrUpdate<RateIngestionJob>(
        "rates-ingestion",
        job => job.RunAsync(CancellationToken.None),
        Cron.Minutely);
    RecurringJob.AddOrUpdate<RateHistoryRollupJob>(
        "rates-history-rollup",
        job => job.RunAsync(CancellationToken.None),
        Cron.Hourly);
}

host.Run();
