using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Remito.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin")]
public sealed class AdminController : ApiStubControllerBase
{
    [HttpGet("exchanges"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult ListExchanges(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) => NotImplementedYet();

    [HttpPost("exchanges"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult CreateExchange(CancellationToken cancellationToken) => NotImplementedYet();

    [HttpPatch("exchanges/{id:guid}"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult UpdateExchange(Guid id, CancellationToken cancellationToken) => NotImplementedYet();

    [HttpGet("rate-sources"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult ListRateSources(CancellationToken cancellationToken) => NotImplementedYet();

    [HttpPost("rate-sources"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult CreateRateSource(CancellationToken cancellationToken) => NotImplementedYet();

    [HttpPost("rates"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult IngestRate(CancellationToken cancellationToken) => NotImplementedYet();

    [HttpGet("ad-campaigns"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult ListAdCampaigns(CancellationToken cancellationToken) => NotImplementedYet();

    [HttpPost("ad-campaigns"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult CreateAdCampaign(CancellationToken cancellationToken) => NotImplementedYet();

    [HttpGet("news"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult ListNews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) => NotImplementedYet();

    [HttpPost("news"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult CreateNews(CancellationToken cancellationToken) => NotImplementedYet();

    [HttpPatch("news/{id:guid}"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult UpdateNews(Guid id, CancellationToken cancellationToken) => NotImplementedYet();

    [HttpGet("audit-logs"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult ListAuditLogs(
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) => NotImplementedYet();
}
