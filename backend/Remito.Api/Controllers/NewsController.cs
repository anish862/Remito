using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Remito.Application.Engagement;

namespace Remito.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/news")]
public sealed class NewsController(INewsService newsService) : ControllerBase
{
    /// <summary>Serves the legacy ad-slot placeholder route until the ad inventory is implemented.</summary>
    [HttpGet("/api/v{version:apiVersion}/ads/slots/{slot}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult AdSlot(string slot, CancellationToken cancellationToken) =>
        Problem(
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented",
            detail: "This API endpoint is defined by the contract but is not implemented yet.");

    /// <summary>Lists published news items, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NewsResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<NewsResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await newsService.ListAsync(page, pageSize, cancellationToken));

    /// <summary>Gets a published news item by slug.</summary>
    [HttpGet("{slug}")]
    [ProducesResponseType<NewsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NewsResponse>> Get(string slug, CancellationToken cancellationToken) =>
        Ok(await newsService.GetAsync(slug, cancellationToken));
}
