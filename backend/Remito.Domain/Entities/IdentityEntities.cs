namespace Remito.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public required string ExternalId { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public ICollection<Device> Devices { get; } = new List<Device>();
}

public sealed class Device
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string FcmToken { get; set; }
    public DevicePlatform Platform { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public User User { get; set; } = null!;
}

public enum DevicePlatform
{
    Android,
    Ios
}
