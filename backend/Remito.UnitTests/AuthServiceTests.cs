using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Remito.Application.Auth;
using Remito.Domain.Entities;
using Remito.Infrastructure.Auth;
using Remito.Infrastructure.Persistence;

namespace Remito.UnitTests;

public sealed class AuthServiceTests
{
    private static readonly AuthOptions Options = new()
    {
        Issuer = "Remito.Tests",
        Audience = "Remito.Tests.Client",
        SigningKey = "test-signing-key-with-at-least-thirty-two-bytes",
        OtpHashKey = "test-otp-hashing-key-with-at-least-thirty-two-bytes",
        TermsVersion = "terms-2026-01",
        PrivacyVersion = "privacy-2026-01",
        RefreshTokenLifetimeDays = 30
    };

    [Fact]
    public async Task Login_issues_a_fifteen_minute_access_token_and_rotates_refresh_tokens()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.RequestOtpAsync(OtpChannel.Email, "user@example.com", "192.0.2.1", default);

        var firstSession = await fixture.Service.VerifyOtpAsync(
            OtpChannel.Email,
            "user@example.com",
            fixture.Sender.LastCode!,
            "192.0.2.1",
            default);
        var accessToken = new JwtSecurityTokenHandler().ReadJwtToken(firstSession.AccessToken);
        var firstStoredToken = await fixture.DbContext.RefreshTokens.SingleAsync();

        Assert.Equal(900, (int)(accessToken.ValidTo - accessToken.ValidFrom).TotalSeconds);
        Assert.Equal(64, firstStoredToken.TokenHash.Length);
        Assert.DoesNotContain(firstSession.RefreshToken, firstStoredToken.TokenHash);

        var secondSession = await fixture.Service.RefreshAsync(firstSession.RefreshToken, default);
        Assert.NotEqual(firstSession.RefreshToken, secondSession.RefreshToken);

        await Assert.ThrowsAsync<AuthOperationException>(
            () => fixture.Service.RefreshAsync(firstSession.RefreshToken, default));
        await Assert.ThrowsAsync<AuthOperationException>(
            () => fixture.Service.RefreshAsync(secondSession.RefreshToken, default));
        Assert.All(
            await fixture.DbContext.RefreshTokens.ToListAsync(),
            refreshToken => Assert.NotNull(refreshToken.RevokedAt));
    }

    [Fact]
    public async Task Expired_otp_and_refresh_tokens_are_rejected()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.RequestOtpAsync(OtpChannel.Phone, "+96891234567", "192.0.2.2", default);
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));

        await Assert.ThrowsAsync<AuthOperationException>(() => fixture.Service.VerifyOtpAsync(
            OtpChannel.Phone,
            "+96891234567",
            fixture.Sender.LastCode!,
            "192.0.2.2",
            default));

        await fixture.Service.RequestOtpAsync(OtpChannel.Phone, "+96891234568", "192.0.2.3", default);
        var session = await fixture.Service.VerifyOtpAsync(
            OtpChannel.Phone,
            "+96891234568",
            fixture.Sender.LastCode!,
            "192.0.2.3",
            default);
        fixture.Clock.Advance(TimeSpan.FromDays(31));

        await Assert.ThrowsAsync<AuthOperationException>(
            () => fixture.Service.RefreshAsync(session.RefreshToken, default));
    }

    [Fact]
    public async Task Fifth_incorrect_otp_attempt_locks_the_challenge()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.RequestOtpAsync(OtpChannel.Email, "lock@example.com", "192.0.2.4", default);
        var incorrectCode = fixture.Sender.LastCode == "000000" ? "000001" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<AuthOperationException>(() => fixture.Service.VerifyOtpAsync(
                OtpChannel.Email,
                "lock@example.com",
                incorrectCode,
                "192.0.2.4",
                default));
        }

        await Assert.ThrowsAsync<AuthOperationException>(() => fixture.Service.VerifyOtpAsync(
            OtpChannel.Email,
            "lock@example.com",
            fixture.Sender.LastCode!,
            "192.0.2.4",
            default));
        Assert.Equal(5, (await fixture.DbContext.OtpChallenges.SingleAsync()).Attempts);
    }

    [Fact]
    public async Task Consent_changes_are_versioned_audited_and_required_consents_cannot_be_withdrawn()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.RequestOtpAsync(OtpChannel.Email, "consent@example.com", "192.0.2.6", default);
        var session = await fixture.Service.VerifyOtpAsync(
            OtpChannel.Email,
            "consent@example.com",
            fixture.Sender.LastCode!,
            "192.0.2.6",
            default);
        var user = await fixture.DbContext.Users.SingleAsync();

        var terms = await fixture.Service.UpdateConsentAsync(
            user.Id, ConsentType.Terms, true, Options.TermsVersion, default);
        var marketing = await fixture.Service.UpdateConsentAsync(
            user.Id, ConsentType.Marketing, false, "1", default);

        Assert.True(terms.IsGranted);
        Assert.False(marketing.IsGranted);
        Assert.Equal(2, (await fixture.Service.GetConsentsAsync(user.Id, default)).Count);
        Assert.Equal(2, await fixture.DbContext.AuditLogs.CountAsync(log => log.Action == "consent_updated"));
        await Assert.ThrowsAsync<AuthOperationException>(() => fixture.Service.UpdateConsentAsync(
            user.Id, ConsentType.Terms, false, Options.TermsVersion, default));
        await Assert.ThrowsAsync<AuthOperationException>(() => fixture.Service.UpdateConsentAsync(
            user.Id, ConsentType.Privacy, true, "stale-version", default));
        Assert.NotEmpty(session.AccessToken);
    }

    [Fact]
    public async Task Device_registration_is_idempotent_and_prevents_cross_account_token_reuse()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.RequestOtpAsync(OtpChannel.Email, "device@example.com", "192.0.2.7", default);
        await fixture.Service.VerifyOtpAsync(
            OtpChannel.Email,
            "device@example.com",
            fixture.Sender.LastCode!,
            "192.0.2.7",
            default);
        var user = await fixture.DbContext.Users.SingleAsync();
        var first = await fixture.Service.RegisterDeviceAsync(
            user.Id, "fcm-token-value", DevicePlatform.Android, default);
        var update = await fixture.Service.RegisterDeviceAsync(
            user.Id, "fcm-token-value", DevicePlatform.Ios, default);

        Assert.Equal(first.Id, update.Id);
        Assert.Equal(DevicePlatform.Ios, update.Platform);
        Assert.Equal(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("fcm-token-value"))),
            (await fixture.DbContext.Devices.SingleAsync()).FcmTokenHash);

        var otherUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalId = "second-user",
            CreatedAt = fixture.Clock.GetUtcNow()
        };
        fixture.DbContext.Users.Add(otherUser);
        await fixture.DbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<AuthOperationException>(() =>
            fixture.Service.RegisterDeviceAsync(
                otherUser.Id, "fcm-token-value", DevicePlatform.Android, default));
        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
    }

    [Fact]
    public async Task Account_deletion_removes_personal_identifiers_and_anonymises_analytics()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.RequestOtpAsync(OtpChannel.Email, "delete@example.com", "192.0.2.5", default);
        var session = await fixture.Service.VerifyOtpAsync(
            OtpChannel.Email,
            "delete@example.com",
            fixture.Sender.LastCode!,
            "192.0.2.5",
            default);
        var user = await fixture.DbContext.Users.SingleAsync();
        var exchange = new Exchange
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Slug = "test",
            Status = ExchangeStatus.Active,
            Tier = ExchangeTier.Standard
        };
        var corridor = new Corridor
        {
            Id = Guid.NewGuid(),
            FromCurrency = "USD",
            ToCurrency = "INR",
            IsActive = true
        };
        fixture.DbContext.Exchanges.Add(exchange);
        fixture.DbContext.Corridors.AddRange(
            corridor,
            new Corridor
            {
                Id = Guid.NewGuid(),
                FromCurrency = "USD",
                ToCurrency = "AED",
                IsActive = true
            });
        fixture.DbContext.RedirectEvents.Add(new RedirectEvent
        {
            Id = Guid.NewGuid(),
            ClickId = "click-1",
            UserId = user.Id,
            ExchangeId = exchange.Id,
            CorridorId = corridor.Id,
            Amount = 2,
            CreatedAt = fixture.Clock.GetUtcNow(),
            Status = RedirectStatus.Created
        });
        var campaign = new AdCampaign
        {
            Id = Guid.NewGuid(),
            Name = "Campaign",
            StartsAt = fixture.Clock.GetUtcNow().AddDays(-1),
            EndsAt = fixture.Clock.GetUtcNow().AddDays(1),
            IsActive = true
        };
        var creative = new AdCreative
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            Slot = "home",
            ImageUrl = "https://example.test/image",
            TargetUrl = "https://example.test",
            AltText = "Sponsored",
            SortOrder = 0
        };
        fixture.DbContext.AdCampaigns.Add(campaign);
        fixture.DbContext.AdCreatives.Add(creative);
        fixture.DbContext.AdImpressions.Add(new AdImpression
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CreativeId = creative.Id,
            CreatedAt = fixture.Clock.GetUtcNow()
        });
        fixture.DbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "login",
            EntityType = "User",
            EntityId = user.Id.ToString(),
            CreatedAt = fixture.Clock.GetUtcNow()
        });
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Service.DeleteAccountAsync(user.Id, default);

        Assert.Null(user.Email);
        Assert.Null(user.PhoneNumber);
        Assert.Null(user.DisplayName);
        Assert.NotNull(user.DeletedAt);
        Assert.Null((await fixture.DbContext.RedirectEvents.SingleAsync()).UserId);
        Assert.Null((await fixture.DbContext.AdImpressions.SingleAsync()).UserId);
        Assert.All(await fixture.DbContext.AuditLogs.ToListAsync(), log => Assert.Null(log.UserId));
        await Assert.ThrowsAsync<AuthOperationException>(
            () => fixture.Service.GetProfileAsync(user.Id, default));
        await Assert.ThrowsAsync<AuthOperationException>(
            () => fixture.Service.RefreshAsync(session.RefreshToken, default));
    }

    private static async Task<AuthFixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbContext = new RemitoDbContext(
            new DbContextOptionsBuilder<RemitoDbContext>().UseSqlite(connection).Options);
        await dbContext.Database.EnsureCreatedAsync();
        var sender = new CapturingOtpSender();
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-10-20T12:00:00Z"));
        var service = new AuthService(
            dbContext,
            sender,
            new AllowAllRateLimiter(),
            Microsoft.Extensions.Options.Options.Create(Options),
            clock);
        return new AuthFixture(connection, dbContext, sender, clock, service);
    }

    private sealed class AuthFixture(
        SqliteConnection connection,
        RemitoDbContext dbContext,
        CapturingOtpSender sender,
        MutableTimeProvider clock,
        AuthService service) : IAsyncDisposable
    {
        public RemitoDbContext DbContext { get; } = dbContext;
        public CapturingOtpSender Sender { get; } = sender;
        public MutableTimeProvider Clock { get; } = clock;
        public AuthService Service { get; } = service;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
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
            LastCode = code;
            return Task.CompletedTask;
        }
    }

    private sealed class AllowAllRateLimiter : IOtpRateLimiter
    {
        public Task<OtpRateLimitResult> CheckLimitAsync(
            string scope,
            string subjectHash,
            string ipAddress,
            int subjectLimit,
            int ipLimit,
            TimeSpan window,
            CancellationToken cancellationToken) =>
            Task.FromResult(new OtpRateLimitResult(true, subjectLimit - 1, window));
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}
