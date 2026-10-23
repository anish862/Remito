using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Remito.Api.Controllers;

public static class AnalyticsEventTaxonomy
{
    private static readonly HashSet<string> EventNames =
    [
        "screen_view",
        "compare_run",
        "exchange_selected",
        "redirect_started",
        "redirect_returned",
        "alert_created",
        "notification_opened",
        "ad_impression",
        "ad_click"
    ];

    public static bool TryHashInstallationId(string value, out string installationHash)
    {
        installationHash = string.Empty;
        if (!Guid.TryParse(value, out var installationId)
            || installationId.ToString("N")[12] != '4')
        {
            return false;
        }

        installationHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(installationId.ToString("D"))));
        return true;
    }

    public static bool IsSupportedEvent(string eventName) => EventNames.Contains(eventName);
}

public sealed record AnalyticsEventRequest
{
    [Required, RegularExpression("^[a-fA-F0-9-]{36}$")]
    public required string InstallationId { get; init; }

    [Required, StringLength(40)]
    public required string EventName { get; init; }
}

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/analytics")]
public sealed class AnalyticsController(TelemetryClient telemetryClient) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("events")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public IActionResult Ingest([FromBody] AnalyticsEventRequest request)
    {
        if (!AnalyticsEventTaxonomy.TryHashInstallationId(request.InstallationId, out var installationHash)
            || !AnalyticsEventTaxonomy.IsSupportedEvent(request.EventName))
        {
            return BadRequest();
        }

        telemetryClient.TrackEvent(
            request.EventName,
            new Dictionary<string, string> { ["installation_hash"] = installationHash });
        return Accepted();
    }
}
