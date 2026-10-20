using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Remito.Application.Auth;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace Remito.IntegrationTests;

public sealed class AuthApiIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer sqlServer = new MsSqlBuilder()
        .WithEnvironment("MSSQL_MEMORY_LIMIT_MB", "2048")
        .Build();
    private AuthApiFactory factory = null!;

    public async Task InitializeAsync()
    {
        await sqlServer.StartAsync();
        factory = new AuthApiFactory(sqlServer.GetConnectionString());
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RemitoDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await factory.DisposeAsync();
        await sqlServer.DisposeAsync();
    }

    [Fact]
    public async Task Otp_login_profile_consent_device_and_refresh_replay_are_handled_end_to_end()
    {
        using var client = factory.CreateClient();
        using var requestResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/request",
            new { channel = "email", destination = "integration@example.com" });
        Assert.Equal(HttpStatusCode.Accepted, requestResponse.StatusCode);

        var sender = factory.Services.GetRequiredService<CapturingOtpSender>();
        using var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new { channel = "email", destination = "integration@example.com", code = sender.LastCode });
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        using var sessionJson = JsonDocument.Parse(await verifyResponse.Content.ReadAsStringAsync());
        var accessToken = sessionJson.RootElement.GetProperty("accessToken").GetString()!;
        var firstRefreshToken = sessionJson.RootElement.GetProperty("refreshToken").GetString()!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var profileResponse = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        using var profileJson = JsonDocument.Parse(await profileResponse.Content.ReadAsStringAsync());
        Assert.Equal("integration@example.com", profileJson.RootElement.GetProperty("email").GetString());

        using var consentResponse = await client.PutAsJsonAsync(
            "/api/v1/me/consents/terms",
            new { isGranted = true, version = "1" });
        Assert.Equal(HttpStatusCode.OK, consentResponse.StatusCode);

        using var deviceResponse = await client.PostAsJsonAsync(
            "/api/v1/devices",
            new { fcmToken = "test-fcm-token", platform = "android" });
        Assert.Equal(HttpStatusCode.Created, deviceResponse.StatusCode);

        using var refreshResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = firstRefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        using var refreshedJson = JsonDocument.Parse(await refreshResponse.Content.ReadAsStringAsync());
        var rotatedRefreshToken = refreshedJson.RootElement.GetProperty("refreshToken").GetString()!;

        using var replayResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = firstRefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);

        using var familyTokenResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = rotatedRefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, familyTokenResponse.StatusCode);

        using var logoutResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new { refreshToken = rotatedRefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        await using var dbScope = factory.Services.CreateAsyncScope();
        var dbContext = dbScope.ServiceProvider.GetRequiredService<RemitoDbContext>();
        Assert.Contains(await dbContext.AuditLogs.Select(log => log.Action).ToListAsync(), action => action == "login");
        Assert.Contains(await dbContext.AuditLogs.Select(log => log.Action).ToListAsync(), action => action == "consent_updated");
        Assert.Contains(await dbContext.AuditLogs.Select(log => log.Action).ToListAsync(), action => action == "logout");
    }

    private sealed class AuthApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = connectionString
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<RemitoDbContext>();
                services.RemoveAll<DbContextOptions<RemitoDbContext>>();
                services.AddDbContext<RemitoDbContext>(options => options.UseSqlServer(connectionString));
                services.RemoveAll<IOtpSender>();
                services.AddSingleton<CapturingOtpSender>();
                services.AddSingleton<IOtpSender>(provider => provider.GetRequiredService<CapturingOtpSender>());
                services.RemoveAll<IOtpRateLimiter>();
                services.AddSingleton<IOtpRateLimiter, AllowAllOtpRateLimiter>();
            });
        }
    }

    private sealed class CapturingOtpSender : IOtpSender
    {
        public string? LastCode { get; private set; }

        public Task SendAsync(
            OtpChannel channel,
            string destination,
            string code,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCode = code;
            return Task.CompletedTask;
        }
    }

    private sealed class AllowAllOtpRateLimiter : IOtpRateLimiter
    {
        public Task<OtpRateLimitResult> CheckLimitAsync(
            string scope,
            string subjectHash,
            string ipAddress,
            int subjectLimit,
            int ipLimit,
            TimeSpan window,
            CancellationToken cancellationToken) =>
            Task.FromResult(new OtpRateLimitResult(true, subjectLimit, window));
    }
}
