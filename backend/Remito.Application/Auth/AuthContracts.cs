using Remito.Domain.Entities;

namespace Remito.Application.Auth;

public interface IAuthService
{
    Task RequestOtpAsync(OtpChannel channel, string destination, string ipAddress, CancellationToken cancellationToken);
    Task<AuthSession> VerifyOtpAsync(
        OtpChannel channel,
        string destination,
        string code,
        string ipAddress,
        CancellationToken cancellationToken);
    Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
    Task<UserProfile> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserProfile> UpdateProfileAsync(Guid userId, string displayName, CancellationToken cancellationToken);
    Task DeleteAccountAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConsentState>> GetConsentsAsync(Guid userId, CancellationToken cancellationToken);
    Task<ConsentState> UpdateConsentAsync(
        Guid userId,
        ConsentType type,
        bool isGranted,
        string version,
        CancellationToken cancellationToken);
    Task<DeviceRegistrationResult> RegisterDeviceAsync(
        Guid userId,
        string fcmToken,
        DevicePlatform platform,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<DeviceRegistrationResult>> ListDevicesAsync(Guid userId, CancellationToken cancellationToken);
    Task RemoveDeviceAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken);
}

public interface IOtpSender
{
    Task SendAsync(OtpChannel channel, string destination, string code, CancellationToken cancellationToken);
}

public interface IOtpRateLimiter
{
    Task<OtpRateLimitResult> CheckLimitAsync(
        string scope,
        string subjectHash,
        string ipAddress,
        int subjectLimit,
        int ipLimit,
        TimeSpan window,
        CancellationToken cancellationToken);
}

public sealed record AuthSession(string AccessToken, string RefreshToken, int ExpiresIn);

public sealed record UserProfile(
    Guid Id,
    string? Email,
    string? PhoneNumber,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt);

public sealed record ConsentState(
    ConsentType Type,
    string Version,
    bool IsGranted,
    DateTimeOffset UpdatedAt);

public sealed record DeviceRegistrationResult(Guid Id, DevicePlatform Platform, DateTimeOffset CreatedAt);

public sealed record OtpRateLimitResult(bool IsAllowed, int Remaining, TimeSpan RetryAfter);

public sealed class AuthOperationException(
    string message,
    int statusCode,
    int? rateLimit = null,
    int? rateLimitRemaining = null,
    TimeSpan? retryAfter = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public int? RateLimit { get; } = rateLimit;
    public int? RateLimitRemaining { get; } = rateLimitRemaining;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
