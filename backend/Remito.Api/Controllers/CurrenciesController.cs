using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Remito.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/currencies")]
public sealed class CurrenciesController : ApiStubControllerBase
{
    [HttpGet, ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult List(CancellationToken cancellationToken) => NotImplementedYet();

    [HttpGet("{code}"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Get(string code, CancellationToken cancellationToken) => NotImplementedYet();
}
