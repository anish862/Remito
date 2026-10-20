using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remito.Application.Auth;
using Remito.Domain.Entities;

namespace Remito.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("auth/otp/request")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RequestOtp(
        [FromBody] OtpRequest request,
        CancellationToken cancellationToken)
    {
        await authService.RequestOtpAsync(
            request.Channel,
            request.Destination,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            cancellationToken);
        return Accepted(new OtpRequestResponse(true));
    }

    [AllowAnonymous]
    [HttpPost("auth/otp/verify")]
    [ProducesResponseType<AuthSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthSessionResponse>> VerifyOtp(
        [FromBody] OtpVerificationRequest request,
        CancellationToken cancellationToken)
    {
        var session = await authService.VerifyOtpAsync(
            request.Channel,
            request.Destination,
            request.Code,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            cancellationToken);
        return Ok(new AuthSessionResponse(session.AccessToken, session.RefreshToken, "Bearer", session.ExpiresIn));
    }

    [AllowAnonymous]
    [HttpPost("auth/refresh")]
    [ProducesResponseType<AuthSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthSessionResponse>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var session = await authService.RefreshAsync(request.RefreshToken, cancellationToken);
        return Ok(new AuthSessionResponse(session.AccessToken, session.RefreshToken, "Bearer", session.ExpiresIn));
    }

    [AllowAnonymous]
    [HttpPost("auth/logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<UserProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfile>> GetMe(CancellationToken cancellationToken)
    {
        var profile = await authService.GetProfileAsync(GetUserId(), cancellationToken);
        return Ok(profile);
    }

    [Authorize]
    [HttpPatch("me")]
    [ProducesResponseType<UserProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserProfile>> PatchMe(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await authService.UpdateProfileAsync(GetUserId(), request.DisplayName, cancellationToken);
        return Ok(profile);
    }

    [Authorize]
    [HttpDelete("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteMe(CancellationToken cancellationToken)
    {
        await authService.DeleteAccountAsync(GetUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me/consents")]
    [ProducesResponseType<IReadOnlyList<ConsentState>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConsentState>>> GetConsents(CancellationToken cancellationToken)
    {
        var consents = await authService.GetConsentsAsync(GetUserId(), cancellationToken);
        return Ok(consents);
    }

    [Authorize]
    [HttpPut("me/consents/terms")]
    [ProducesResponseType<ConsentState>(StatusCodes.Status200OK)]
    public Task<ActionResult<ConsentState>> UpdateTermsConsent(
        [FromBody] ConsentRequest request,
        CancellationToken cancellationToken) =>
        UpdateConsent(ConsentType.Terms, request, cancellationToken);

    [Authorize]
    [HttpPut("me/consents/privacy")]
    [ProducesResponseType<ConsentState>(StatusCodes.Status200OK)]
    public Task<ActionResult<ConsentState>> UpdatePrivacyConsent(
        [FromBody] ConsentRequest request,
        CancellationToken cancellationToken) =>
        UpdateConsent(ConsentType.Privacy, request, cancellationToken);

    [Authorize]
    [HttpPut("me/consents/marketing")]
    [ProducesResponseType<ConsentState>(StatusCodes.Status200OK)]
    public Task<ActionResult<ConsentState>> UpdateMarketingConsent(
        [FromBody] ConsentRequest request,
        CancellationToken cancellationToken) =>
        UpdateConsent(ConsentType.Marketing, request, cancellationToken);

    [Authorize]
    [HttpPost("devices")]
    [ProducesResponseType<DeviceRegistrationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeviceRegistrationResponse>> RegisterDevice(
        [FromBody] DeviceRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var device = await authService.RegisterDeviceAsync(
            GetUserId(),
            request.FcmToken,
            request.Platform,
            cancellationToken);
        var response = new DeviceRegistrationResponse(device.Id, device.Platform, device.CreatedAt);
        return Created($"/api/v1/devices/{device.Id}", response);
    }

    private async Task<ActionResult<ConsentState>> UpdateConsent(
        ConsentType type,
        ConsentRequest request,
        CancellationToken cancellationToken)
    {
        var consent = await authService.UpdateConsentAsync(
            GetUserId(),
            type,
            request.IsGranted,
            request.Version,
            cancellationToken);
        return Ok(consent);
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new AuthOperationException("The access token is invalid.", StatusCodes.Status401Unauthorized);
    }
}

/// <summary>Requests a one-time sign-in code for an email address or E.164 phone number.</summary>
public sealed record OtpRequest
{
    /// <summary>Delivery channel.</summary>
    [Required]
    public required OtpChannel Channel { get; init; }

    /// <summary>Email address or E.164 phone number.</summary>
    [Required, MaxLength(320)]
    public required string Destination { get; init; }
}

/// <summary>Confirms a six-digit one-time sign-in code.</summary>
public sealed record OtpVerificationRequest
{
    /// <summary>Delivery channel used to request the code.</summary>
    [Required]
    public required OtpChannel Channel { get; init; }

    /// <summary>Email address or E.164 phone number.</summary>
    [Required, MaxLength(320)]
    public required string Destination { get; init; }

    /// <summary>Six-digit one-time code.</summary>
    [Required, RegularExpression("^[0-9]{6}$")]
    public required string Code { get; init; }
}

/// <summary>Refresh token to rotate or revoke.</summary>
public sealed record RefreshRequest
{
    /// <summary>Opaque refresh token.</summary>
    [Required, MinLength(32), MaxLength(128)]
    public required string RefreshToken { get; init; }
}

/// <summary>Bearer access token and rotating refresh token.</summary>
public sealed record AuthSessionResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn);

/// <summary>Confirms that a one-time code request was accepted.</summary>
public sealed record OtpRequestResponse(bool Accepted);

/// <summary>Profile fields editable by the account owner.</summary>
public sealed record UpdateProfileRequest
{
    /// <summary>Display name.</summary>
    [Required, MaxLength(200)]
    public required string DisplayName { get; init; }
}

/// <summary>Records the current-version consent choice.</summary>
public sealed record ConsentRequest
{
    /// <summary>Whether consent is granted.</summary>
    public bool IsGranted { get; init; }

    /// <summary>Document version shown to the user.</summary>
    [Required, MaxLength(50)]
    public required string Version { get; init; }
}

/// <summary>Registers a device push-notification token.</summary>
public sealed record DeviceRegistrationRequest
{
    /// <summary>Firebase Cloud Messaging token.</summary>
    [Required, MaxLength(4096)]
    public required string FcmToken { get; init; }

    /// <summary>Device operating system.</summary>
    [Required]
    public required DevicePlatform Platform { get; init; }
}

/// <summary>Registered device information.</summary>
public sealed record DeviceRegistrationResponse(Guid Id, DevicePlatform Platform, DateTimeOffset CreatedAt);
