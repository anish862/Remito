using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Remito.Application.Rates;

namespace Remito.Api.Controllers;

/// <summary>Request body for converting an amount between currencies.</summary>
public sealed record ConvertRateRequest
{
    /// <summary>Positive amount to convert, with an upper limit of one trillion.</summary>
    [Required, Range(typeof(decimal), "0.000000001", "1000000000000")]
    public required decimal Amount { get; init; }

    /// <summary>Three-letter ISO 4217 source currency code.</summary>
    [Required, StringLength(3, MinimumLength = 3)]
    public required string From { get; init; }

    /// <summary>Three-letter ISO 4217 destination currency code.</summary>
    [Required, StringLength(3, MinimumLength = 3)]
    public required string To { get; init; }
}

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}")]
public sealed class RatesController(IRateQueryService rateQueryService) : ControllerBase
{
    /// <summary>Gets current rates for one base currency, including their freshness and 24-hour change.</summary>
    [HttpGet("rates/latest")]
    [ProducesResponseType<LatestRatesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Latest(
        [FromQuery] string @base,
        [FromQuery] string symbols,
        CancellationToken cancellationToken)
    {
        var response = await rateQueryService.GetLatestAsync(
            @base,
            symbols.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            cancellationToken);
        Response.Headers.CacheControl = "public, max-age=15";
        var etag = $"\"{System.Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(response)))}\"";
        Response.Headers.ETag = etag;
        if (Request.Headers.TryGetValue("If-None-Match", out var conditionalTags)
            && conditionalTags.ToString().Split(',').Any(candidate =>
                    string.Equals(candidate.Trim(), etag, StringComparison.Ordinal)
                    || string.Equals(candidate.Trim(), "*", StringComparison.Ordinal)))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Ok(response);
    }

    /// <summary>Gets historical rate points and summary statistics for a supported range.</summary>
    [HttpGet("rates/history")]
    [ProducesResponseType<RateHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RateHistoryResponse>> History(
        [FromQuery] string pair,
        [FromQuery] string range,
        CancellationToken cancellationToken)
    {
        var history = await rateQueryService.GetHistoryAsync(pair, range, cancellationToken);
        return Ok(history);
    }

    /// <summary>Converts a positive amount using the current direct or USD cross-rate.</summary>
    [HttpPost("convert")]
    [ProducesResponseType<CurrencyConversionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CurrencyConversionResponse>> Convert(
        [FromBody] ConvertRateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await rateQueryService.ConvertAsync(
            request.Amount,
            request.From,
            request.To,
            cancellationToken);
        return Ok(result);
    }
}
