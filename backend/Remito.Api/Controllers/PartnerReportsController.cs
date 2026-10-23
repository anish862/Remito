using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remito.Application.Engagement;

namespace Remito.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/exchanges/{exchangeId:guid}/reports")]
public sealed class PartnerReportsController(IPartnerAttributionService attributionService) : ControllerBase
{
    /// <summary>Returns monthly redirects, confirmed conversions, conversion rate, and commission.</summary>
    [Authorize(Policy = "PartnerManagement")]
    [HttpGet("monthly")]
    [ProducesResponseType<ExchangeMonthlyReport>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExchangeMonthlyReport>> Monthly(
        Guid exchangeId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken)
    {
        var report = await attributionService.GetMonthlyReportAsync(
            exchangeId,
            year,
            month,
            cancellationToken);
        return Ok(report);
    }
}
