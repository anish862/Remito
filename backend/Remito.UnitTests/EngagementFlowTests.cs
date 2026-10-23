using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Remito.Application.Abstractions;
using Remito.Application.Engagement;
using Remito.Domain.Entities;
using Remito.Infrastructure.Engagement;
using Remito.Infrastructure.Persistence;

namespace Remito.UnitTests;

public sealed class EngagementFlowTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 5, 0, 0, TimeSpan.Zero);
    private static readonly Guid ExchangeId = Guid.Parse("8f5f600e-f80c-422b-ac18-4967b99b9052");
    private static readonly Guid CorridorId = Guid.Parse("0f43b2c9-44b3-4b91-8e7f-2aa9e19ecb01");
    private const string RedirectKey = "test-redirect-signing-key-with-32-bytes";
    private const string PartnerSecret = "test-partner-hmac-secret-with-32-bytes";
    private static int userNumber;

    [Fact]
    public async Task Redirect_creates_one_ulid_event_and_uses_only_allowlisted_privacy_safe_urls()
    {
        await using var fixture = await CreateFixtureAsync();
        var user = fixture.AddUser();
        var response = await fixture.RedirectService().CreateAsync(
            new CreateRedirectRequest(ExchangeId, "OMR-INR", 10m, AppInstalledHint: true),
            user.Id,
            default);

        Assert.Matches("^[0-9A-HJKMNP-TV-Z]{26}$", response.ClickId);
        Assert.Contains("app.exchange.test", response.DestinationUrl, StringComparison.Ordinal);
        Assert.Contains("click_sig=", response.DestinationUrl, StringComparison.Ordinal);
        Assert.Contains("utm_source=remito", response.DestinationUrl, StringComparison.Ordinal);
        Assert.Contains("store.exchange.test", response.FallbackUrl, StringComparison.Ordinal);
        Assert.DoesNotContain(user.Email!, response.DestinationUrl, StringComparison.Ordinal);
        Assert.DoesNotContain(user.PhoneNumber!, response.DestinationUrl, StringComparison.Ordinal);
        Assert.Single(fixture.Context.RedirectEvents);
        var stored = await fixture.Context.RedirectEvents.SingleAsync();
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal(10m, stored.Amount);
    }

    [Fact]
    public async Task Redirect_rejects_open_redirect_destinations_not_on_exchange_allowlist()
    {
        await using var fixture = await CreateFixtureAsync(gatewayUrl: "https://attacker.example/collect");

        var exception = await Assert.ThrowsAsync<EngagementOperationException>(
            () => fixture.RedirectService().CreateAsync(
                new CreateRedirectRequest(ExchangeId, "OMR-INR", 10m),
                null,
                default));

        Assert.Equal(409, exception.StatusCode);
        Assert.Empty(fixture.Context.RedirectEvents);
    }

    [Fact]
    public async Task Signed_postback_is_idempotent_and_creates_one_commission_ledger_entry()
    {
        await using var fixture = await CreateFixtureAsync();
        var redirect = await fixture.RedirectService().CreateAsync(
            new CreateRedirectRequest(ExchangeId, "OMR-INR", 100m),
            null,
            default);
        var body = PostbackBody(redirect.ClickId, "external-1");
        var timestamp = Now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var signature = Sign(PartnerSecret, timestamp, body);
        var service = fixture.AttributionService();

        var first = await service.ProcessPostbackAsync(
            ExchangeId, timestamp, signature, body, Request(redirect.ClickId, "external-1"), default);
        var duplicate = await service.ProcessPostbackAsync(
            ExchangeId, timestamp, signature, body, Request(redirect.ClickId, "external-1"), default);

        Assert.False(first.WasDuplicate);
        Assert.True(duplicate.WasDuplicate);
        Assert.Equal(first.ConversionId, duplicate.ConversionId);
        var ledger = await fixture.Context.CommissionLedgers.SingleAsync();
        Assert.Equal(0.50m, ledger.Amount);
        Assert.Equal("INR", ledger.CurrencyCode);
        var report = await service.GetMonthlyReportAsync(ExchangeId, 2026, 10, default);
        Assert.Equal(1, report.Redirects);
        Assert.Equal(1, report.Conversions);
        Assert.Equal(0.50m, report.Commission);
    }

    [Fact]
    public async Task Postback_rejects_invalid_signature_and_signed_tampered_click_id()
    {
        await using var fixture = await CreateFixtureAsync();
        var redirect = await fixture.RedirectService().CreateAsync(
            new CreateRedirectRequest(ExchangeId, "OMR-INR", 10m),
            null,
            default);
        var service = fixture.AttributionService();
        var timestamp = Now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var body = PostbackBody(redirect.ClickId, "external-2");

        var badSignature = await Assert.ThrowsAsync<EngagementOperationException>(() =>
            service.ProcessPostbackAsync(
                ExchangeId, timestamp, "sha256=00", body, Request(redirect.ClickId, "external-2"), default));
        var tamperedClickId = await Assert.ThrowsAsync<EngagementOperationException>(() =>
            service.ProcessPostbackAsync(
                ExchangeId,
                timestamp,
                Sign(PartnerSecret, timestamp, body.Replace(redirect.ClickId, "tampered-click-id", StringComparison.Ordinal)),
                body.Replace(redirect.ClickId, "tampered-click-id", StringComparison.Ordinal),
                Request("tampered-click-id", "external-2"),
                default));

        Assert.Equal(401, badSignature.StatusCode);
        Assert.Equal(400, tamperedClickId.StatusCode);
        Assert.Empty(fixture.Context.ReferralConversions);
    }

    [Fact]
    public async Task Reconciliation_report_commission_matches_the_ledger_total()
    {
        await using var fixture = await CreateFixtureAsync();
        var redirect = await fixture.RedirectService().CreateAsync(
            new CreateRedirectRequest(ExchangeId, "OMR-INR", 10m),
            null,
            default);
        var body = PostbackBody(redirect.ClickId, "external-report", 10m);
        var timestamp = Now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        await fixture.AttributionService().ProcessPostbackAsync(
            ExchangeId,
            timestamp,
            Sign(PartnerSecret, timestamp, body),
            body,
            Request(redirect.ClickId, "external-report", 10m),
            default);

        var directory = Path.Combine(Path.GetTempPath(), $"remito-reconcile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var csv = string.Join('\n',
                "external_ref,click_id,converted_at,amount,commission,currency",
                $"external-report,{redirect.ClickId},{Now:O},10,0.50,INR");
            await File.WriteAllTextAsync(Path.Combine(directory, $"{ExchangeId:D}_202610.csv"), csv);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PartnerReconciliation:ImportDirectory"] = directory
            }).Build();
            await new PartnerReconciliationJob(
                fixture.Context,
                config,
                new FixedTimeProvider(Now),
                NullLogger<PartnerReconciliationJob>.Instance).RunAsync(default);

            var report = await fixture.AttributionService().GetMonthlyReportAsync(ExchangeId, 2026, 10, default);
            Assert.Equal(report.Commission, report.ReportedCommission);
            Assert.Equal(0m, report.CommissionVariance);
            Assert.Equal(0, report.UnmatchedReportRows);
            Assert.Equal(0, report.MissingPostbacks);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Rate_alert_sends_once_per_crossing_and_rearms_only_after_crossing_back()
    {
        await using var fixture = await CreateFixtureAsync();
        var user = fixture.AddUser(withDevice: true);
        fixture.Context.Alerts.Add(new Alert
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Pair = "OMR/INR",
            Direction = AlertDirection.Above,
            Target = 220m,
            IsActive = true,
            IsArmed = true
        });
        await fixture.Context.SaveChangesAsync();
        var cache = fixture.Cache;
        cache.SetRate("OMR/INR", 221m, Now);
        var job = fixture.AlertJob(cache);

        await job.RunAsync(default);
        await job.RunAsync(default);
        Assert.Single(fixture.PushSender.Notices);
        Assert.Equal(1, await fixture.Context.Alerts.CountAsync(alert => !alert.IsArmed));

        cache.SetRate("OMR/INR", 219m, Now);
        await job.RunAsync(default);
        Assert.True(await fixture.Context.Alerts.SingleAsync() is { IsArmed: true });

        cache.SetRate("OMR/INR", 222m, Now);
        await job.RunAsync(default);
        Assert.Equal(2, fixture.PushSender.Notices.Count);
        Assert.All(fixture.PushSender.Notices, notice =>
            Assert.DoesNotContain(notice.Data.Keys, key => key.Contains("email", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Quiet_hours_and_marketing_consent_gate_push_and_invalid_tokens_are_pruned()
    {
        await using var fixture = await CreateFixtureAsync();
        var quietUser = fixture.AddUser(withDevice: true);
        quietUser.QuietHoursStartMinute = 22 * 60;
        quietUser.QuietHoursEndMinute = 6 * 60;
        await fixture.Context.SaveChangesAsync();
        var preferences = new PushNotificationService(
            fixture.Context,
            fixture.PushSender,
            new FixedTimeProvider(Now));
        var notice = new PushNotice(
            "Rate alert",
            "OMR/INR crossed your alert threshold.",
            new Dictionary<string, string> { ["type"] = "rate_alert", ["pair"] = "OMR/INR" },
            "rate_alert");

        Assert.False(await preferences.SendToUserAsync(quietUser.Id, notice, default));
        Assert.Empty(fixture.PushSender.Notices);

        quietUser.QuietHoursStartMinute = null;
        quietUser.QuietHoursEndMinute = null;
        await fixture.Context.SaveChangesAsync();
        fixture.PushSender.InvalidTokens.Add("fcm-token");
        Assert.True(await preferences.SendToUserAsync(quietUser.Id, notice, default));
        Assert.Empty(await fixture.Context.Devices.ToListAsync());

        var marketingUser = fixture.AddUser(withDevice: true);
        var marketingNotice = notice with { Category = "marketing" };
        Assert.False(await preferences.SendToUserAsync(marketingUser.Id, marketingNotice, default));
        fixture.Context.UserConsents.Add(new UserConsent
        {
            UserId = marketingUser.Id,
            Type = ConsentType.Marketing,
            Version = "1",
            IsGranted = true,
            UpdatedAt = Now
        });
        await fixture.Context.SaveChangesAsync();
        Assert.True(await preferences.SendToUserAsync(marketingUser.Id, marketingNotice, default));
    }

    private static PartnerPostbackRequest Request(string clickId, string externalRef, decimal amount = 100m) =>
        new(clickId, externalRef, Now, amount, ConversionStatus.Confirmed);

    private static string PostbackBody(string clickId, string externalRef, decimal amount = 100m) =>
        JsonSerializer.Serialize(Request(clickId, externalRef, amount), new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        });

    private static string Sign(string secret, string timestamp, string body) =>
        "sha256=" + Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes($"{timestamp}.{body}")));

    private static async Task<Fixture> CreateFixtureAsync(
        string gatewayUrl = "https://gateway.exchange.test/send")
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var context = new RemitoDbContext(
            new DbContextOptionsBuilder<RemitoDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        context.Exchanges.Add(new Exchange
        {
            Id = ExchangeId,
            Name = "Test Exchange",
            Slug = "test-exchange",
            Status = ExchangeStatus.Active,
            Tier = ExchangeTier.Standard,
            HasApp = true,
            AppDeepLink = "testapp://app.exchange.test/open",
            PlayStoreUrl = "https://store.exchange.test/app",
            GatewayUrl = gatewayUrl,
            AllowedRedirectHosts = "gateway.exchange.test,store.exchange.test,app.exchange.test",
            CommissionFlatAmount = 0.50m,
            CommissionCurrencyCode = "INR",
            IsPlaceholder = false
        });
        await context.SaveChangesAsync();
        return new Fixture(connection, context);
    }

    private sealed class Fixture(SqliteConnection connection, RemitoDbContext context) : IAsyncDisposable
    {
        public RemitoDbContext Context { get; } = context;
        public RecordingRateCache Cache { get; } = new();
        public RecordingPushSender PushSender { get; } = new();

        public User AddUser(bool withDevice = false)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                ExternalId = Guid.NewGuid().ToString("N"),
                Email = $"private-{Guid.NewGuid():N}@example.test",
                PhoneNumber = $"+968{Interlocked.Increment(ref userNumber):D8}",
                CreatedAt = Now
            };
            Context.Users.Add(user);
            if (withDevice)
            {
                const string token = "fcm-token";
                Context.Devices.Add(new Device
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    FcmToken = token,
                    FcmTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
                    Platform = DevicePlatform.Android,
                    CreatedAt = Now
                });
            }

            Context.SaveChanges();
            return user;
        }

        public RedirectService RedirectService()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redirect:SigningKey"] = RedirectKey
            }).Build();
            return new RedirectService(Context, configuration, new FixedTimeProvider(Now));
        }

        public PartnerAttributionService AttributionService()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"PartnerPostback:Secrets:{ExchangeId:D}"] = PartnerSecret
            }).Build();
            return new PartnerAttributionService(Context, configuration, new FixedTimeProvider(Now));
        }

        public RateAlertEvaluationJob AlertJob(RecordingRateCache cache) =>
            new(Context, cache, new PushNotificationService(Context, PushSender, new FixedTimeProvider(Now)),
                new FixedTimeProvider(Now), NullLogger<RateAlertEvaluationJob>.Instance);

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class RecordingRateCache : IRateCache
    {
        private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);

        public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            values.TryGetValue(key, out var value);
            return ValueTask.FromResult(value);
        }

        public ValueTask SetAsync(
            string key,
            string value,
            TimeSpan lifetime,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            values[key] = value;
            return ValueTask.CompletedTask;
        }

        public void SetRate(string pair, decimal mid, DateTimeOffset fetchedAt) =>
            values[$"rates:latest:{pair.Replace("/", "-", StringComparison.Ordinal)}"] =
                JsonSerializer.Serialize(new
                {
                    Pair = pair,
                    Mid = mid,
                    FetchedAt = fetchedAt,
                    IsStale = false,
                    StalenessMinutes = 15
                });
    }

    private sealed class RecordingPushSender : IPushSender
    {
        public List<PushNotice> Notices { get; } = [];
        public HashSet<string> InvalidTokens { get; } = new(StringComparer.Ordinal);

        public Task<PushBatchResult> SendAsync(
            IReadOnlyList<string> tokens,
            PushNotice notice,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Notices.Add(notice);
            return Task.FromResult(new PushBatchResult(
                InvalidTokens.Intersect(tokens, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal)));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
