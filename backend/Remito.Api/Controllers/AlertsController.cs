using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Remito.Application.Auth;
using Remito.Application.Engagement;

namespace Remito.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/alerts")]
public sealed class AlertsController(IAlertService alertService) : ControllerBase
{
    [HttpGet, ProducesResponseType<IReadOnlyList<AlertResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AlertResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await alertService.ListAsync(GetUserId(), page, pageSize, cancellationToken));

    [HttpPost, ProducesResponseType<AlertResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AlertResponse>> Create(
        [FromBody] SaveAlertRequest request,
        CancellationToken cancellationToken)
    {
        var alert = await alertService.CreateAsync(GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = alert.Id }, alert);
    }

    [HttpPatch("{id:guid}"), ProducesResponseType<AlertResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AlertResponse>> Update(
        Guid id,
        [FromBody] SaveAlertRequest request,
        CancellationToken cancellationToken) =>
        Ok(await alertService.UpdateAsync(GetUserId(), id, request, cancellationToken));

    [HttpDelete("{id:guid}"), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await alertService.DeleteAsync(GetUserId(), id, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : throw new AuthOperationException("The access token is invalid.", StatusCodes.Status401Unauthorized);
}
