using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Remito.Application.Engagement;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Api.Controllers;

public sealed class AdsOptions
{
    public bool Enabled { get; set; }
    public int DailyFrequencyCap { get; set; } = 3;
}

public sealed record AdSlotResponse(
    string Label,
    Guid CreativeId,
    string Slot,
    string ImageUrl,
    string TargetUrl,
    string AltText,
    bool IsPersonalized);

public sealed record AdEventRequest
{
    [Required]
    public Guid CreativeId { get; init; }

    [Required, RegularExpression("^(impression|click)$")]
    public required string EventType { get; init; }
}

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/ads")]
public sealed class AdsController(
    RemitoDbContext dbContext,
    IOptionsMonitor<AdsOptions> options,
    TimeProvider timeProvider,
    Microsoft.ApplicationInsights.TelemetryClient telemetryClient) : ControllerBase
{
    private static readonly HashSet<string> AllowedSlots =
        new(StringComparer.OrdinalIgnoreCase) { "banner", "native", "interstitial", "rewarded" };

    /// <summary>Returns an eligible contextual creative. The redirect confirmation sheet is not an ad slot.</summary>
    [Authorize]
    [HttpGet("slots")]
    [ProducesResponseType<AdSlotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<ActionResult<AdSlotResponse>> GetSlot(
        [FromQuery] string slot,
        CancellationToken cancellationToken) => SelectSlot(slot, cancellationToken);

    [Authorize]
    [HttpGet("slots/{slot}")]
    [ProducesResponseType<AdSlotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<ActionResult<AdSlotResponse>> GetSlotByName(
        string slot,
        CancellationToken cancellationToken) => SelectSlot(slot, cancellationToken);

    /// <summary>Records a delivered impression or click without accepting user-provided event properties.</summary>
    [Authorize]
    [HttpPost("events")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordEvent(
        [FromBody] AdEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.Enabled)
        {
            return NotFound();
        }

        var creative = await dbContext.AdCreatives
            .Include(item => item.Campaign)
            .SingleOrDefaultAsync(item => item.Id == request.CreativeId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (creative is null
            || !creative.Campaign.IsActive
            || creative.Campaign.StartsAt > now
            || creative.Campaign.EndsAt <= now)
        {
            return NotFound();
        }

        var userId = GetOptionalUserId();
        if (request.EventType == "impression")
        {
            dbContext.AdImpressions.Add(new AdImpression
            {
                Id = Guid.NewGuid(),
                CreativeId = creative.Id,
                UserId = userId,
                CreatedAt = now
            });
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = request.EventType == "impression" ? "ad_impression" : "ad_click",
            EntityType = nameof(AdCreative),
            EntityId = creative.Id.ToString(),
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        telemetryClient.TrackEvent(request.EventType == "impression" ? "ad_impression" : "ad_click");
        return NoContent();
    }

    private async Task<ActionResult<AdSlotResponse>> SelectSlot(
        string slot,
        CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.Enabled)
        {
            return NoContent();
        }

        if (string.IsNullOrWhiteSpace(slot) || !AllowedSlots.Contains(slot))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Unsupported ad slot",
                detail: "Supported slots are banner, native, interstitial, and rewarded.");
        }

        slot = slot.ToLowerInvariant();
        var userId = GetOptionalUserId();
        var now = timeProvider.GetUtcNow();
        var candidates = await dbContext.AdCreatives
            .AsNoTracking()
            .Include(item => item.Campaign)
            .Where(item => item.Slot == slot
                && item.Campaign.IsActive
                && item.Campaign.StartsAt <= now
                && item.Campaign.EndsAt > now)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Campaign.StartsAt)
            .ToListAsync(cancellationToken);

        if (userId is null)
        {
            return NoContent();
        }

        var cap = Math.Clamp(options.CurrentValue.DailyFrequencyCap, 1, 100);
        foreach (var creative in candidates)
        {
            var recentImpressions = await dbContext.AdImpressions.CountAsync(
                impression => impression.CreativeId == creative.Id
                    && impression.UserId == userId
                    && impression.CreatedAt > now.AddHours(-24),
                cancellationToken);
            if (AdFrequencyCap.HasReached(recentImpressions, cap))
            {
                continue;
            }

            return Ok(new AdSlotResponse(
                "Ad",
                creative.Id,
                creative.Slot,
                creative.ImageUrl,
                creative.TargetUrl,
                creative.AltText,
                IsPersonalized: false));
        }

        return NoContent();
    }

    private Guid? GetOptionalUserId()
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out var userId) ? userId : null;
    }
}
