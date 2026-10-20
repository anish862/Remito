using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Remito.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}")]
public sealed class RatesController : ApiStubControllerBase
{
    [HttpGet("rates/latest"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Latest([FromQuery] string pair, CancellationToken cancellationToken) => NotImplementedYet();

    [HttpGet("rates/history"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult History(
        [FromQuery] string pair,
        [FromQuery] string bucket,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) => NotImplementedYet();

    [HttpGet("convert"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Convert(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] decimal amount,
        CancellationToken cancellationToken) => NotImplementedYet();
}
