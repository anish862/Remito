using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Remito.Domain.Entities;

namespace Remito.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(user => user.Id);
        builder.Property(user => user.ExternalId).HasMaxLength(200).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(320);
        builder.Property(user => user.PhoneNumber).HasMaxLength(16);
        builder.Property(user => user.DisplayName).HasMaxLength(200);
        builder.HasIndex(user => user.ExternalId).IsUnique();
        builder.HasIndex(user => user.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
        builder.HasIndex(user => user.PhoneNumber).IsUnique().HasFilter("[PhoneNumber] IS NOT NULL");
        builder.HasIndex(user => user.DeletedAt);
    }

    internal sealed class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
    {
        public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
        {
            builder.HasKey(role => new { role.UserId, role.Role });
            builder.Property(role => role.Role).HasConversion<string>().HasMaxLength(30);
            builder.HasOne(role => role.User).WithMany(user => user.Roles)
                .HasForeignKey(role => role.UserId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    internal sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
    {
        public void Configure(EntityTypeBuilder<OtpChallenge> builder)
        {
            builder.HasKey(challenge => challenge.Id);
            builder.Property(challenge => challenge.DestinationHash).HasMaxLength(64).IsFixedLength().IsRequired();
            builder.Property(challenge => challenge.Channel).HasConversion<string>().HasMaxLength(10);
            builder.Property(challenge => challenge.CodeHash).HasMaxLength(64).IsFixedLength().IsRequired();
            builder.HasIndex(challenge => new { challenge.DestinationHash, challenge.CreatedAt });
            builder.HasIndex(challenge => challenge.ExpiresAt);
        }
    }

    internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.HasKey(token => token.Id);
            builder.Property(token => token.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
            builder.HasIndex(token => token.TokenHash).IsUnique();
            builder.HasIndex(token => new { token.FamilyId, token.RevokedAt });
            builder.HasIndex(token => token.UserId);
            builder.HasOne(token => token.User).WithMany().HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<RefreshToken>().WithMany().HasForeignKey(token => token.ReplacedById)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    internal sealed class UserConsentConfiguration : IEntityTypeConfiguration<UserConsent>
    {
        public void Configure(EntityTypeBuilder<UserConsent> builder)
        {
            builder.HasKey(consent => new { consent.UserId, consent.Type });
            builder.Property(consent => consent.Type).HasConversion<string>().HasMaxLength(20);
            builder.Property(consent => consent.Version).HasMaxLength(50).IsRequired();
            builder.HasOne(consent => consent.User).WithMany().HasForeignKey(consent => consent.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.HasKey(device => device.Id);
        builder.Property(device => device.FcmToken).IsRequired();
        builder.Property(device => device.FcmTokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(device => device.Platform).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(device => device.FcmTokenHash).IsUnique();
        builder.HasIndex(device => new { device.UserId, device.Platform });
        builder.HasOne(device => device.User).WithMany(user => user.Devices)
            .HasForeignKey(device => device.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
