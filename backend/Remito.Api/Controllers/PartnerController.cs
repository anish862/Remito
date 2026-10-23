using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remito.Application.Engagement;

namespace Remito.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/partners")]
public sealed class PartnerController(IPartnerAttributionService attributionService) : ControllerBase
{
    private static readonly JsonSerializerOptions PostbackJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>Accepts a timestamped HMAC-SHA256 partner conversion postback.</summary>
    [AllowAnonymous]
    [HttpPost("{exchangeId:guid}/postback")]
    [RequestSizeLimit(16_384)]
    [ProducesResponseType<PartnerPostbackResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PartnerPostbackResponse>> Postback(
        Guid exchangeId,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        PartnerPostbackRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<PartnerPostbackRequest>(rawBody, PostbackJsonOptions);
        }
        catch (JsonException)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid partner payload",
                Detail = "The postback body is not valid JSON.",
                Instance = Request.Path
            });
        }

        if (request is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid partner payload",
                Detail = "A postback body is required.",
                Instance = Request.Path
            });
        }

        var result = await attributionService.ProcessPostbackAsync(
            exchangeId,
            Request.Headers["X-Partner-Timestamp"].ToString(),
            Request.Headers["X-Partner-Signature"].ToString(),
            rawBody,
            request,
            cancellationToken);
        return Ok(result);
    }
}
