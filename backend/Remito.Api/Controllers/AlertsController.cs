using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Remito.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/alerts")]
public sealed class AlertsController : ApiStubControllerBase
{
    [HttpGet, ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) => NotImplementedYet();

    [HttpPost, ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Create(CancellationToken cancellationToken) => NotImplementedYet();

    [HttpPatch("{id:guid}"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Update(Guid id, CancellationToken cancellationToken) => NotImplementedYet();

    [HttpDelete("{id:guid}"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Delete(Guid id, CancellationToken cancellationToken) => NotImplementedYet();
}
