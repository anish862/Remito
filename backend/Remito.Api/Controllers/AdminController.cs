using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin")]
public sealed class AdminController(
    RemitoDbContext dbContext,
    TimeProvider timeProvider) : ApiStubControllerBase
{
    [Authorize(Policy = "PartnerManagement")]
    [HttpGet("exchanges"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult ListExchanges(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) => NotImplementedYet();

    [Authorize(Policy = "PartnerManagement")]
    [HttpPost("exchanges"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult CreateExchange(CancellationToken cancellationToken) => NotImplementedYet();

    [Authorize(Policy = "PartnerManagement")]
    [HttpPatch("exchanges/{id:guid}"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult UpdateExchange(Guid id, CancellationToken cancellationToken) => NotImplementedYet();

    [Authorize(Policy = "PartnerManagement")]
    [HttpGet("rate-sources"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult ListRateSources(CancellationToken cancellationToken) => NotImplementedYet();

    [Authorize(Policy = "PartnerManagement")]
    [HttpPost("rate-sources"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult CreateRateSource(CancellationToken cancellationToken) => NotImplementedYet();

    [Authorize(Policy = "PartnerManagement")]
    [HttpPost("rates"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult IngestRate(CancellationToken cancellationToken) => NotImplementedYet();

    [Authorize(Policy = "ReadOnly")]
    [HttpGet("ad-campaigns")]
    [ProducesResponseType<IReadOnlyList<AdCampaignResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdCampaignResponse>>> ListAdCampaigns(
        CancellationToken cancellationToken)
    {
        var campaigns = await dbContext.AdCampaigns
            .AsNoTracking()
            .OrderByDescending(campaign => campaign.StartsAt)
            .Select(campaign => new AdCampaignResponse(
                campaign.Id,
                campaign.Name,
                campaign.StartsAt,
                campaign.EndsAt,
                campaign.IsActive,
                dbContext.AdCreatives
                    .Where(creative => creative.CampaignId == campaign.Id)
                    .Select(creative => new AdCreativeResponse(
                        creative.Id,
                        creative.Slot,
                        creative.ImageUrl,
                        creative.TargetUrl,
                        creative.AltText,
                        creative.SortOrder))
                    .ToList()))
            .ToListAsync(cancellationToken);
        return Ok(campaigns);
    }

    [Authorize(Policy = "AdsManagement")]
    [HttpPost("ad-campaigns")]
    [ProducesResponseType<AdCampaignResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdCampaignResponse>> CreateAdCampaign(
        [FromBody] CreateAdCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.Creative.AltText)
            || request.StartsAt >= request.EndsAt
            || request.EndsAt <= now
            || !new[] { "banner", "native", "interstitial", "rewarded" }
                .Contains(request.Creative.Slot, StringComparer.OrdinalIgnoreCase)
            || !IsSafeHttpsUrl(request.Creative.ImageUrl)
            || !IsSafeHttpsUrl(request.Creative.TargetUrl))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid campaign",
                Detail = "Campaign dates, slot, creative image URL, or destination URL are invalid."
            });
        }

        var campaign = new AdCampaign
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            IsActive = request.IsActive
        };
        var creative = new AdCreative
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            Slot = request.Creative.Slot.ToLowerInvariant(),
            ImageUrl = request.Creative.ImageUrl,
            TargetUrl = request.Creative.TargetUrl,
            AltText = request.Creative.AltText.Trim(),
            SortOrder = 0
        };
        dbContext.AdCampaigns.Add(campaign);
        dbContext.AdCreatives.Add(creative);
        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = GetActorId(),
            Action = "ad_campaign_created",
            EntityType = nameof(AdCampaign),
            EntityId = campaign.Id.ToString(),
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new AdCampaignResponse(
            campaign.Id,
            campaign.Name,
            campaign.StartsAt,
            campaign.EndsAt,
            campaign.IsActive,
            [new AdCreativeResponse(
                creative.Id,
                creative.Slot,
                creative.ImageUrl,
                creative.TargetUrl,
                creative.AltText,
                creative.SortOrder)]);
        return Created("/api/v1/admin/ad-campaigns", response);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("news"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult ListNews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) => NotImplementedYet();

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("news"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult CreateNews(CancellationToken cancellationToken) => NotImplementedYet();

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("news/{id:guid}"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult UpdateNews(Guid id, CancellationToken cancellationToken) => NotImplementedYet();

    [Authorize(Policy = "ReadOnly")]
    [HttpGet("audit-logs"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult ListAuditLogs(
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) => NotImplementedYet();

    private Guid? GetActorId() =>
        Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId)
            ? userId
            : null;

    private static bool IsSafeHttpsUrl(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo);
}

public sealed record CreateAdCampaignRequest
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset EndsAt { get; init; }
    public bool IsActive { get; init; } = true;

    [System.ComponentModel.DataAnnotations.Required]
    public required CreateAdCreativeRequest Creative { get; init; }
}

public sealed record CreateAdCreativeRequest
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(20)]
    public required string Slot { get; init; }

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.Url,
     System.ComponentModel.DataAnnotations.StringLength(2048)]
    public required string ImageUrl { get; init; }

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.Url,
     System.ComponentModel.DataAnnotations.StringLength(2048)]
    public required string TargetUrl { get; init; }

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(500)]
    public required string AltText { get; init; }
}

public sealed record AdCampaignResponse(
    Guid Id,
    string Name,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsActive,
    IReadOnlyList<AdCreativeResponse> Creatives);

public sealed record AdCreativeResponse(
    Guid Id,
    string Slot,
    string ImageUrl,
    string TargetUrl,
    string AltText,
    int SortOrder);
