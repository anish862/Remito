using System.Security.Claims;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Remito.Api.Controllers;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace Remito.IntegrationTests;

public sealed class AdDeliveryIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer sqlServer = new MsSqlBuilder()
        .WithEnvironment("MSSQL_MEMORY_LIMIT_MB", "2048")
        .Build();

    public Task InitializeAsync() => sqlServer.StartAsync();

    public Task DisposeAsync() => sqlServer.DisposeAsync().AsTask();

    [Fact]
    public async Task Selection_skips_capped_creative_and_stops_serving_after_impression_cap()
    {
        var now = new DateTimeOffset(2026, 10, 23, 17, 0, 0, TimeSpan.Zero);
        var userId = Guid.NewGuid();
        var cappedCreativeId = Guid.NewGuid();
        var eligibleCreativeId = Guid.NewGuid();
        var connectionString = new SqlConnectionStringBuilder(sqlServer.GetConnectionString())
        {
            InitialCatalog = "RemitoAdsTests"
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<RemitoDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using (var setupContext = new RemitoDbContext(options))
        {
            await setupContext.Database.MigrateAsync();
            var user = new User
            {
                Id = userId,
                ExternalId = Guid.NewGuid().ToString("N"),
                CreatedAt = now.AddDays(-1)
            };
            var campaign = new AdCampaign
            {
                Id = Guid.NewGuid(),
                Name = "frequency cap test",
                StartsAt = now.AddHours(-1),
                EndsAt = now.AddHours(1),
                IsActive = true
            };
            setupContext.Users.Add(user);
            setupContext.AdCampaigns.Add(campaign);
            setupContext.AdCreatives.AddRange(
                CreateCreative(cappedCreativeId, campaign.Id, campaign, 0),
                CreateCreative(eligibleCreativeId, campaign.Id, campaign, 1));
            setupContext.AdImpressions.AddRange(
                Enumerable.Range(0, 3).Select(_ => new AdImpression
                {
                    Id = Guid.NewGuid(),
                    CreativeId = cappedCreativeId,
                    UserId = userId,
                    CreatedAt = now.AddHours(-1)
                }));
            await setupContext.SaveChangesAsync();
        }

        await using var dbContext = new RemitoDbContext(options);
        using var telemetryConfiguration = TelemetryConfiguration.CreateDefault();
        var controller = new AdsController(
            dbContext,
            new TestOptionsMonitor<AdsOptions>(new AdsOptions { Enabled = true, DailyFrequencyCap = 3 }),
            new FixedTimeProvider(now),
            new TelemetryClient(telemetryConfiguration))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                        "test"))
                }
            }
        };

        var nextCreative = await controller.GetSlotByName("banner", CancellationToken.None);
        var ad = Assert.IsType<AdSlotResponse>(Assert.IsType<OkObjectResult>(nextCreative.Result).Value);
        Assert.Equal(eligibleCreativeId, ad.CreativeId);
        Assert.Equal("Ad", ad.Label);
        Assert.False(ad.IsPersonalized);

        for (var impression = 0; impression < 3; impression++)
        {
            var eventResult = await controller.RecordEvent(
                new AdEventRequest { CreativeId = eligibleCreativeId, EventType = "impression" },
                CancellationToken.None);
            Assert.IsType<NoContentResult>(eventResult);
        }

        var capped = await controller.GetSlotByName("banner", CancellationToken.None);
        Assert.IsType<NoContentResult>(capped.Result);
        Assert.Equal(3, await dbContext.AdImpressions.CountAsync(
            impression => impression.CreativeId == eligibleCreativeId));
        Assert.Equal(3, await dbContext.AuditLogs.CountAsync(log => log.Action == "ad_impression"));

        var adminController = new AdminController(dbContext, new FixedTimeProvider(now))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                            new Claim(ClaimTypes.Role, "AdsManager")
                        ],
                        "test"))
                }
            }
        };
        var createResult = await adminController.CreateAdCampaign(
            new CreateAdCampaignRequest
            {
                Name = "new admin campaign",
                StartsAt = now.AddMinutes(-1),
                EndsAt = now.AddHours(2),
                Creative = new CreateAdCreativeRequest
                {
                    Slot = "native",
                    ImageUrl = "https://cdn.example/native.png",
                    TargetUrl = "https://partner.example/native",
                    AltText = "Native partner offer"
                }
            },
            CancellationToken.None);
        Assert.IsType<CreatedResult>(createResult.Result);
        Assert.Single(await dbContext.AuditLogs
            .Where(log => log.Action == "ad_campaign_created")
            .ToListAsync());
        var campaignListResult = await adminController.ListAdCampaigns(CancellationToken.None);
        var campaigns = Assert.IsType<OkObjectResult>(campaignListResult.Result);
        Assert.Contains(
            Assert.IsAssignableFrom<IReadOnlyList<AdCampaignResponse>>(campaigns.Value),
            campaign => campaign.Name == "new admin campaign");
    }

    private static AdCreative CreateCreative(
        Guid creativeId,
        Guid campaignId,
        AdCampaign campaign,
        int sortOrder) =>
        new()
        {
            Id = creativeId,
            CampaignId = campaignId,
            Campaign = campaign,
            Slot = "banner",
            ImageUrl = $"https://cdn.example/{creativeId:N}.png",
            TargetUrl = "https://partner.example/",
            AltText = "Partner offer",
            SortOrder = sortOrder
        };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestOptionsMonitor<TOptions>(TOptions currentValue) : IOptionsMonitor<TOptions>
    {
        public TOptions CurrentValue => currentValue;

        public TOptions Get(string? name) => currentValue;

        public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
    }
}
