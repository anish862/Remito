using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remito.Application.Engagement;

namespace Remito.Api.Controllers;

/// <summary>Input for creating a referral redirect.</summary>
public sealed record CreateRedirectBody
{
    /// <summary>Active exchange that owns the destination.</summary>
    [Required]
    public required Guid ExchangeId { get; init; }

    /// <summary>Active currency corridor in BASE-QUOTE format.</summary>
    [Required, StringLength(7, MinimumLength = 7)]
    public required string Corridor { get; init; }

    /// <summary>Transfer amount in the source currency.</summary>
    [Range(typeof(decimal), "0.01", "1000000000000")]
    public required decimal Amount { get; init; }

    /// <summary>Client hint used only to select the configured app deep link.</summary>
    public bool AppInstalledHint { get; init; }
}

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/redirect")]
public sealed class RedirectController(IRedirectService redirectService) : ControllerBase
{
    /// <summary>Creates a privacy-safe referral click and returns the allow-listed destination.</summary>
    [AllowAnonymous]
    [HttpPost]
    [ProducesResponseType<RedirectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RedirectResponse>> Create(
        [FromBody] CreateRedirectBody request,
        CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? userId = Guid.TryParse(subject, out var parsedUserId) ? parsedUserId : null;
        var response = await redirectService.CreateAsync(
            new CreateRedirectRequest(
                request.ExchangeId,
                request.Corridor,
                request.Amount,
                request.AppInstalledHint),
            userId,
            cancellationToken);
        return Ok(response);
    }
}
