using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Remito.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}")]
public sealed class NewsController : ApiStubControllerBase
{
    [HttpGet("news"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) => NotImplementedYet();

    [HttpGet("news/{slug}"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Get(string slug, CancellationToken cancellationToken) => NotImplementedYet();

    [HttpGet("ads/slots/{slot}"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult AdSlot(string slot, CancellationToken cancellationToken) => NotImplementedYet();
}
