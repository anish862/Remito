using Hangfire;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddHangfire(configuration => configuration.UseSqlServerStorage(connectionString));
    builder.Services.AddHangfireServer();
}

var host = builder.Build();
host.Run();
