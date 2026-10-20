using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Remito.Application.Auth;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Infrastructure.Auth;

public sealed class AuthService(
    RemitoDbContext dbContext,
    IOtpSender otpSender,
    IOtpRateLimiter rateLimiter,
    IOptions<AuthOptions> options,
    TimeProvider timeProvider) : IAuthService
{
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OtpRateWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan JwtLifetime = TimeSpan.FromMinutes(15);
    private readonly AuthOptions authOptions = options.Value;

    public async Task RequestOtpAsync(
        OtpChannel channel,
        string destination,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var normalizedDestination = NormalizeDestination(channel, destination);
        var destinationHash = HashDestination(channel, normalizedDestination);
        var limit = await rateLimiter.CheckLimitAsync(
            "request",
            destinationHash,
            ipAddress,
            subjectLimit: 5,
            ipLimit: 20,
            OtpRateWindow,
            cancellationToken);

        if (!limit.IsAllowed)
        {
            throw RateLimited(subjectLimit: 5, limit);
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var challengeId = Guid.NewGuid();
        var challenge = new OtpChallenge
        {
            Id = challengeId,
            DestinationHash = destinationHash,
            Channel = channel,
            CodeHash = HashOtp(challengeId, code),
            CreatedAt = timeProvider.GetUtcNow(),
            ExpiresAt = timeProvider.GetUtcNow().Add(OtpLifetime)
        };
        dbContext.OtpChallenges.Add(challenge);
        await dbContext.SaveChangesAsync(cancellationToken);

        await otpSender.SendAsync(channel, normalizedDestination, code, cancellationToken);
    }

    public async Task<AuthSession> VerifyOtpAsync(
        OtpChannel channel,
        string destination,
        string code,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var normalizedDestination = NormalizeDestination(channel, destination);
        var destinationHash = HashDestination(channel, normalizedDestination);
        await EnsureRateAllowedAsync("verify", destinationHash, ipAddress, cancellationToken);
        if (code.Length != 6 || code.Any(character => character is < '0' or > '9'))
        {
            throw InvalidCredentials();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        var challenges = await dbContext.OtpChallenges
            .Where(candidate => candidate.DestinationHash == destinationHash && candidate.Channel == channel)
            .ToListAsync(cancellationToken);
        var challenge = challenges.MaxBy(candidate => candidate.CreatedAt);

        if (challenge is null
            || challenge.ConsumedAt is not null
            || challenge.Attempts >= 5
            || challenge.ExpiresAt <= now)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw InvalidCredentials();
        }

        var suppliedHash = HashOtp(challenge.Id, code);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(suppliedHash),
                Convert.FromHexString(challenge.CodeHash)))
        {
            challenge.Attempts++;
            if (challenge.Attempts >= 5)
            {
                challenge.ConsumedAt = now;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw InvalidCredentials();
        }

        challenge.ConsumedAt = now;
        var user = channel == OtpChannel.Email
            ? await dbContext.Users.Include(candidate => candidate.Roles)
                .FirstOrDefaultAsync(candidate => candidate.Email == normalizedDestination, cancellationToken)
            : await dbContext.Users.Include(candidate => candidate.Roles)
                .FirstOrDefaultAsync(candidate => candidate.PhoneNumber == normalizedDestination, cancellationToken);

        if (user?.DeletedAt is not null)
        {
            user = null;
        }

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                ExternalId = $"otp:{destinationHash}",
                Email = channel == OtpChannel.Email ? normalizedDestination : null,
                PhoneNumber = channel == OtpChannel.Phone ? normalizedDestination : null,
                CreatedAt = now
            };
            user.Roles.Add(new UserRoleAssignment { UserId = user.Id, Role = ApplicationRole.User, User = user });
            dbContext.Users.Add(user);
        }
        else if (user.Roles.Count == 0)
        {
            user.Roles.Add(new UserRoleAssignment { UserId = user.Id, Role = ApplicationRole.User, User = user });
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "login",
            EntityType = nameof(User),
            EntityId = user.Id.ToString(),
            CreatedAt = now
        });
        var session = CreateSession(user, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return session;
    }

    public async Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = HashRefreshToken(refreshToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw InvalidCredentials();
        }

        if (storedToken.RevokedAt is not null)
        {
            await RevokeFamilyAsync(storedToken.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw InvalidCredentials();
        }

        if (storedToken.ExpiresAt <= now)
        {
            await RevokeFamilyAsync(storedToken.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw InvalidCredentials();
        }

        var user = await dbContext.Users.Include(candidate => candidate.Roles)
            .FirstOrDefaultAsync(candidate => candidate.Id == storedToken.UserId && candidate.DeletedAt == null, cancellationToken);
        if (user is null)
        {
            await RevokeFamilyAsync(storedToken.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw InvalidCredentials();
        }

        storedToken.RevokedAt = now;
        var session = CreateSession(user, now, storedToken.FamilyId, storedToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return session;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = HashRefreshToken(refreshToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var token = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);
        if (token is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        var now = timeProvider.GetUtcNow();
        await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = token.UserId,
            Action = "logout",
            EntityType = nameof(User),
            EntityId = token.UserId.ToString(),
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<UserProfile> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking()
            .Include(candidate => candidate.Roles)
            .FirstOrDefaultAsync(candidate => candidate.Id == userId && candidate.DeletedAt == null, cancellationToken);
        return user is null ? throw NotFound() : ToProfile(user);
    }

    public async Task<UserProfile> UpdateProfileAsync(
        Guid userId,
        string displayName,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(candidate => candidate.Roles)
            .FirstOrDefaultAsync(candidate => candidate.Id == userId && candidate.DeletedAt == null, cancellationToken);
        if (user is null)
        {
            throw NotFound();
        }

        user.DisplayName = displayName.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToProfile(user);
    }

    public async Task DeleteAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await dbContext.Users.FirstOrDefaultAsync(
            candidate => candidate.Id == userId && candidate.DeletedAt == null,
            cancellationToken);
        if (user is null)
        {
            throw NotFound();
        }

        var now = timeProvider.GetUtcNow();
        var emailHash = user.Email is null ? null : HashDestination(OtpChannel.Email, user.Email);
        var phoneHash = user.PhoneNumber is null ? null : HashDestination(OtpChannel.Phone, user.PhoneNumber);
        var userTokens = await dbContext.RefreshTokens.Where(token => token.UserId == userId).ToListAsync(cancellationToken);
        foreach (var token in userTokens)
        {
            token.RevokedAt ??= now;
        }

        var challenges = await dbContext.OtpChallenges
            .Where(challenge => (emailHash != null && challenge.DestinationHash == emailHash)
                || (phoneHash != null && challenge.DestinationHash == phoneHash))
            .ToListAsync(cancellationToken);
        dbContext.OtpChallenges.RemoveRange(challenges);
        dbContext.Devices.RemoveRange(await dbContext.Devices.Where(device => device.UserId == userId).ToListAsync(cancellationToken));
        dbContext.UserRoles.RemoveRange(await dbContext.UserRoles.Where(role => role.UserId == userId).ToListAsync(cancellationToken));
        dbContext.UserConsents.RemoveRange(await dbContext.UserConsents.Where(consent => consent.UserId == userId).ToListAsync(cancellationToken));
        dbContext.Favourites.RemoveRange(await dbContext.Favourites.Where(favourite => favourite.UserId == userId).ToListAsync(cancellationToken));
        dbContext.Alerts.RemoveRange(await dbContext.Alerts.Where(alert => alert.UserId == userId).ToListAsync(cancellationToken));

        foreach (var redirect in await dbContext.RedirectEvents.Where(item => item.UserId == userId).ToListAsync(cancellationToken))
        {
            redirect.UserId = null;
        }

        foreach (var impression in await dbContext.AdImpressions.Where(item => item.UserId == userId).ToListAsync(cancellationToken))
        {
            impression.UserId = null;
        }

        foreach (var auditLog in await dbContext.AuditLogs.Where(item => item.UserId == userId).ToListAsync(cancellationToken))
        {
            auditLog.UserId = null;
            auditLog.EntityId = null;
            auditLog.Details = null;
        }

        user.Email = null;
        user.PhoneNumber = null;
        user.DisplayName = null;
        user.ExternalId = $"deleted:{user.Id:N}";
        user.DeletedAt = now;
        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = "account_deleted",
            EntityType = nameof(User),
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConsentState>> GetConsentsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await EnsureActiveUserAsync(userId, cancellationToken);
        return await dbContext.UserConsents.AsNoTracking()
            .Where(consent => consent.UserId == userId)
            .OrderBy(consent => consent.Type)
            .Select(consent => new ConsentState(
                consent.Type,
                consent.Version,
                consent.IsGranted,
                consent.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<ConsentState> UpdateConsentAsync(
        Guid userId,
        ConsentType type,
        bool isGranted,
        string version,
        CancellationToken cancellationToken)
    {
        if (type is ConsentType.Terms or ConsentType.Privacy && !isGranted)
        {
            throw new AuthOperationException(
                "Terms and privacy consent cannot be withdrawn while the account is active.",
                StatusCodes.Status400BadRequest);
        }

        var requiredVersion = type switch
        {
            ConsentType.Terms => authOptions.TermsVersion,
            ConsentType.Privacy => authOptions.PrivacyVersion,
            ConsentType.Marketing => "1",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
        if (!string.Equals(version, requiredVersion, StringComparison.Ordinal))
        {
            throw new AuthOperationException("The consent version is not current.", StatusCodes.Status400BadRequest);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await EnsureActiveUserAsync(userId, cancellationToken);
        var consent = await dbContext.UserConsents
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId && candidate.Type == type, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (consent is null)
        {
            consent = new UserConsent
            {
                UserId = userId,
                Type = type,
                Version = version,
                IsGranted = isGranted,
                UpdatedAt = now
            };
            dbContext.UserConsents.Add(consent);
        }
        else
        {
            consent.Version = version;
            consent.IsGranted = isGranted;
            consent.UpdatedAt = now;
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "consent_updated",
            EntityType = nameof(UserConsent),
            EntityId = $"{type}:{version}",
            CreatedAt = now,
            Details = $"Granted={isGranted}"
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ConsentState(type, version, isGranted, now);
    }

    public async Task<DeviceRegistrationResult> RegisterDeviceAsync(
        Guid userId,
        string fcmToken,
        DevicePlatform platform,
        CancellationToken cancellationToken)
    {
        await EnsureActiveUserAsync(userId, cancellationToken);
        var tokenHash = HashFcmToken(fcmToken);
        var existing = await dbContext.Devices.FirstOrDefaultAsync(
            device => device.FcmTokenHash == tokenHash,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (existing is not null)
        {
            if (existing.UserId != userId)
            {
                throw new AuthOperationException("The device token is already registered.", StatusCodes.Status409Conflict);
            }

            existing.Platform = platform;
            existing.CreatedAt = now;
        }
        else
        {
            existing = new Device
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FcmToken = fcmToken,
                FcmTokenHash = tokenHash,
                Platform = platform,
                CreatedAt = now
            };
            dbContext.Devices.Add(existing);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new DeviceRegistrationResult(existing.Id, existing.Platform, existing.CreatedAt);
    }

    private async Task EnsureRateAllowedAsync(
        string scope,
        string subjectHash,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var limit = await rateLimiter.CheckLimitAsync(
            scope,
            subjectHash,
            ipAddress,
            subjectLimit: 10,
            ipLimit: 30,
            OtpRateWindow,
            cancellationToken);
        if (!limit.IsAllowed)
        {
            throw RateLimited(subjectLimit: 10, limit);
        }
    }

    private static AuthOperationException RateLimited(int subjectLimit, OtpRateLimitResult limit) =>
        new(
            "OTP rate limit exceeded.",
            StatusCodes.Status429TooManyRequests,
            subjectLimit,
            limit.Remaining,
            limit.RetryAfter);

    private AuthSession CreateSession(User user, DateTimeOffset now, Guid? familyId = null, RefreshToken? replacedToken = null)
    {
        var family = familyId ?? Guid.NewGuid();
        var refreshRaw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var refresh = new RefreshToken
        {
            Id = Guid.NewGuid(),
            FamilyId = family,
            UserId = user.Id,
            TokenHash = HashRefreshToken(refreshRaw),
            CreatedAt = now,
            ExpiresAt = now.AddDays(authOptions.RefreshTokenLifetimeDays)
        };
        dbContext.RefreshTokens.Add(refresh);
        if (replacedToken is not null)
        {
            replacedToken.ReplacedById = refresh.Id;
        }

        return new AuthSession(CreateAccessToken(user, now), refreshRaw, (int)JwtLifetime.TotalSeconds);
    }

    private string CreateAccessToken(User user, DateTimeOffset now)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role.Role.ToString())));
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            authOptions.Issuer,
            authOptions.Audience,
            claims,
            now.UtcDateTime,
            now.Add(JwtLifetime).UtcDateTime,
            credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var familyTokens = await dbContext.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in familyTokens)
        {
            token.RevokedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureActiveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(
                user => user.Id == userId && user.DeletedAt == null,
                cancellationToken))
        {
            throw NotFound();
        }
    }

    private string NormalizeDestination(OtpChannel channel, string destination)
    {
        var value = destination.Trim();
        if (channel == OtpChannel.Email)
        {
            value = value.ToLowerInvariant();
            if (!System.Net.Mail.MailAddress.TryCreate(value, out var address)
                || !string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase))
            {
                throw new AuthOperationException("A valid email address is required.", StatusCodes.Status400BadRequest);
            }

            return value;
        }

        if (value.Length is < 8 or > 16 || value[0] != '+' || value.Skip(1).Any(character => !char.IsAsciiDigit(character)))
        {
            throw new AuthOperationException("A phone number in E.164 format is required.", StatusCodes.Status400BadRequest);
        }

        return value;
    }

    private string HashDestination(OtpChannel channel, string destination) =>
        Hmac($"{channel}:{destination}");

    private string HashOtp(Guid challengeId, string code) =>
        Hmac($"{challengeId:N}:{code}");

    private string Hmac(string value) =>
        Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(authOptions.OtpHashKey),
            Encoding.UTF8.GetBytes(value)));

    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private static string HashFcmToken(string fcmToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fcmToken)));

    private static UserProfile ToProfile(User user) =>
        new(
            user.Id,
            user.Email,
            user.PhoneNumber,
            user.DisplayName,
            user.Roles.Select(role => role.Role.ToString()).ToArray(),
            user.CreatedAt);

    private static AuthOperationException InvalidCredentials() =>
        new("The OTP or refresh token is invalid or expired.", StatusCodes.Status401Unauthorized);

    private static AuthOperationException NotFound() =>
        new("The requested account was not found.", StatusCodes.Status404NotFound);
}
