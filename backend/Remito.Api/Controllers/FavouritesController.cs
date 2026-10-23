using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remito.Application.Auth;
using Remito.Application.Engagement;

namespace Remito.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/favourites")]
public sealed class FavouritesController(IFavouriteService favouriteService) : ControllerBase
{
    [HttpGet, ProducesResponseType<IReadOnlyList<FavouriteResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FavouriteResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await favouriteService.ListAsync(GetUserId(), page, pageSize, cancellationToken));

    [HttpPost, ProducesResponseType<FavouriteResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<FavouriteResponse>> Create(
        [FromBody] CreateFavouriteRequest request,
        CancellationToken cancellationToken)
    {
        var favourite = await favouriteService.AddAsync(GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = favourite.Id }, favourite);
    }

    [HttpDelete("{id:guid}"), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await favouriteService.RemoveAsync(GetUserId(), id, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : throw new AuthOperationException("The access token is invalid.", StatusCodes.Status401Unauthorized);
}
