using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Remito.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController : ApiStubControllerBase
{
    [HttpPost("token"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult CreateToken(CancellationToken cancellationToken) => NotImplementedYet();

    [HttpPost("refresh"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult RefreshToken(CancellationToken cancellationToken) => NotImplementedYet();

    [HttpPost("logout"), ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Logout(CancellationToken cancellationToken) => NotImplementedYet();
}
