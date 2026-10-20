namespace Remito.Infrastructure.Auth;

public sealed class AuthOptions
{
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public string OtpHashKey { get; init; } = string.Empty;
    public string TermsVersion { get; init; } = "1";
    public string PrivacyVersion { get; init; } = "1";
    public int RefreshTokenLifetimeDays { get; init; } = 30;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Issuer)
        && !string.IsNullOrWhiteSpace(Audience)
        && System.Text.Encoding.UTF8.GetByteCount(SigningKey) >= 32
        && System.Text.Encoding.UTF8.GetByteCount(OtpHashKey) >= 32
        && RefreshTokenLifetimeDays is > 0 and <= 365;
}
