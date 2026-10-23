using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remito.Application.Auth;

namespace Remito.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/devices")]
public sealed class DevicesController(IAuthService authService) : ControllerBase
{
    [HttpGet, ProducesResponseType<IReadOnlyList<DeviceRegistrationResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DeviceRegistrationResponse>>> List(CancellationToken cancellationToken)
    {
        var devices = await authService.ListDevicesAsync(GetUserId(), cancellationToken);
        return Ok(devices.Select(device =>
            new DeviceRegistrationResponse(device.Id, device.Platform, device.CreatedAt)).ToArray());
    }

    [HttpDelete("{id:guid}"), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken)
    {
        await authService.RemoveDeviceAsync(GetUserId(), id, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : throw new AuthOperationException("The access token is invalid.", StatusCodes.Status401Unauthorized);
}
