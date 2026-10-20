using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Remito.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}")]
public sealed class ExchangesController : ApiStubControllerBase
{
    [HttpGet("exchanges"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) => NotImplementedYet();

    [HttpGet("exchanges/{slug}"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Get(string slug, CancellationToken cancellationToken) => NotImplementedYet();

    [HttpGet("compare"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Compare(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] decimal amount,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) => NotImplementedYet();
}
