using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Remito.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/devices")]
public sealed class DevicesController : ApiStubControllerBase
{
    [HttpGet, ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult List(CancellationToken cancellationToken) => NotImplementedYet();

    [HttpDelete("{id:guid}"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Remove(Guid id, CancellationToken cancellationToken) => NotImplementedYet();
}
