using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Remito.Application.Auth;
using Remito.Application.Rates;
using Remito.Application.Exchanges;

namespace Remito.Api.Errors;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is RateQueryException rateException)
        {
            httpContext.Response.StatusCode = rateException.StatusCode;
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = rateException.StatusCode,
                    Title = rateException.StatusCode == StatusCodes.Status404NotFound
                        ? "Rate not found"
                        : "Invalid rate request",
                    Detail = rateException.Message,
                    Instance = httpContext.Request.Path
                },
                options: null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);
            return true;
        }

        if (exception is ExchangeComparisonException comparisonException)
        {
            httpContext.Response.StatusCode = comparisonException.StatusCode;
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = comparisonException.StatusCode,
                    Title = comparisonException.StatusCode == StatusCodes.Status404NotFound
                        ? "Exchange corridor not found"
                        : "Invalid exchange comparison request",
                    Detail = comparisonException.Message,
                    Instance = httpContext.Request.Path
                },
                options: null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);
            return true;
        }

        if (exception is AuthOperationException authException)
        {
            httpContext.Response.StatusCode = authException.StatusCode;
            httpContext.Response.ContentType = "application/problem+json";
            if (authException.StatusCode == StatusCodes.Status429TooManyRequests)
            {
                var retryAfter = Math.Max(1, (int)Math.Ceiling(authException.RetryAfter?.TotalSeconds ?? 1));
                httpContext.Response.Headers.RetryAfter = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
                httpContext.Response.Headers["X-RateLimit-Limit"] =
                    authException.RateLimit?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0";
                httpContext.Response.Headers["X-RateLimit-Remaining"] =
                    authException.RateLimitRemaining?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0";
                httpContext.Response.Headers["X-RateLimit-Reset"] =
                    DateTimeOffset.UtcNow.AddSeconds(retryAfter).ToUnixTimeSeconds()
                        .ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = authException.StatusCode,
                Title = authException.StatusCode switch
                {
                    StatusCodes.Status400BadRequest => "Invalid request",
                    StatusCodes.Status401Unauthorized => "Unauthorized",
                    StatusCodes.Status404NotFound => "Not found",
                    StatusCodes.Status409Conflict => "Conflict",
                    StatusCodes.Status429TooManyRequests => "Too many requests",
                    _ => "Request failed"
                },
                Detail = authException.Message,
                Instance = httpContext.Request.Path
            }, cancellationToken);
            return true;
        }

        logger.LogError("Unhandled exception for request {TraceId}", httpContext.TraceIdentifier);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            Instance = httpContext.Request.Path
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
