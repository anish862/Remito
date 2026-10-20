using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Remito.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/redirect")]
public sealed class RedirectController : ApiStubControllerBase
{
    [HttpPost("{exchangeId:guid}"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Create(Guid exchangeId, CancellationToken cancellationToken) => NotImplementedYet();
}
