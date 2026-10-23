using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Remito.Application.Exchanges;

namespace Remito.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}")]
public sealed class ExchangesController(IExchangeComparisonService exchangeComparisonService) : ApiStubControllerBase
{
    /// <summary>Lists active exchanges and their current offer for the requested corridor.</summary>
    [HttpGet("exchanges")]
    [ProducesResponseType<ExchangeComparisonResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExchangeComparisonResponse>> List(
        [FromQuery] string from = "OMR",
        [FromQuery] string to = "INR",
        [FromQuery] decimal amount = 10m,
        [FromQuery] string? sort = null,
        [FromQuery] bool? hasApp = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var response = await exchangeComparisonService.GetExchangesAsync(
            from,
            to,
            amount,
            new ExchangeComparisonOptions(sort, hasApp, search, page, pageSize),
            cancellationToken);
        return Ok(response);
    }

    /// <summary>Compares active exchange offers for a corridor and transfer amount.</summary>
    [HttpGet("compare")]
    [ProducesResponseType<ExchangeComparisonResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExchangeComparisonResponse>> Compare(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] decimal amount,
        [FromQuery] string? sort = null,
        [FromQuery] bool? hasApp = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var response = await exchangeComparisonService.GetExchangesAsync(
            from,
            to,
            amount,
            new ExchangeComparisonOptions(sort, hasApp, search, page, pageSize),
            cancellationToken);
        return Ok(response);
    }

    /// <summary>Gets exchange details by slug.</summary>
    [HttpGet("exchanges/{slug}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Get(string slug, CancellationToken cancellationToken) => NotImplementedYet();
}
