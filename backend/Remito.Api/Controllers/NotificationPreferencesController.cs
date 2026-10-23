using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remito.Application.Auth;
using Remito.Application.Engagement;

namespace Remito.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/notification-preferences")]
public sealed class NotificationPreferencesController(
    INotificationPreferencesService preferencesService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<NotificationPreferencesResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPreferencesResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await preferencesService.GetAsync(GetUserId(), cancellationToken));

    [HttpPut]
    [ProducesResponseType<NotificationPreferencesResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPreferencesResponse>> Update(
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await preferencesService.UpdateAsync(GetUserId(), request, cancellationToken));

    private Guid GetUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : throw new AuthOperationException("The access token is invalid.", StatusCodes.Status401Unauthorized);
}
