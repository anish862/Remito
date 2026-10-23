namespace Remito.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public required string ExternalId { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool RateAlertsEnabled { get; set; } = true;
    public bool DailySummaryEnabled { get; set; }
    public int? QuietHoursStartMinute { get; set; }
    public int? QuietHoursEndMinute { get; set; }
    public DateTimeOffset? LastAlertSummaryAt { get; set; }
    public ICollection<Device> Devices { get; } = new List<Device>();
    public ICollection<UserRoleAssignment> Roles { get; } = new List<UserRoleAssignment>();
}

public sealed class Device
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string FcmToken { get; set; }
    public required string FcmTokenHash { get; set; }
    public DevicePlatform Platform { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public User User { get; set; } = null!;
}

public enum DevicePlatform
{
    Android,
    Ios
}

public sealed class UserRoleAssignment
{
    public Guid UserId { get; set; }
    public ApplicationRole Role { get; set; }
    public User User { get; set; } = null!;
}

public enum ApplicationRole
{
    User,
    Admin,
    PartnerManager,
    AdsManager,
    ReadOnly
}

public sealed class OtpChallenge
{
    public Guid Id { get; set; }
    public required string DestinationHash { get; set; }
    public OtpChannel Channel { get; set; }
    public required string CodeHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

public enum OtpChannel
{
    Email,
    Phone
}

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedById { get; set; }
    public User User { get; set; } = null!;
}

public sealed class UserConsent
{
    public Guid UserId { get; set; }
    public ConsentType Type { get; set; }
    public required string Version { get; set; }
    public bool IsGranted { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public User User { get; set; } = null!;
}

public enum ConsentType
{
    Terms,
    Privacy,
    Marketing
}
