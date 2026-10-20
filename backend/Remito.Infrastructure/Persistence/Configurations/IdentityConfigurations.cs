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
        builder.Property(user => user.DisplayName).HasMaxLength(200);
        builder.HasIndex(user => user.ExternalId).IsUnique();
        builder.HasIndex(user => user.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
        builder.HasIndex(user => user.DeletedAt);
    }
}

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.HasKey(device => device.Id);
        builder.Property(device => device.FcmToken).HasMaxLength(4096).IsRequired();
        builder.Property(device => device.Platform).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(device => device.FcmToken).IsUnique();
        builder.HasIndex(device => new { device.UserId, device.Platform });
        builder.HasOne(device => device.User).WithMany(user => user.Devices)
            .HasForeignKey(device => device.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
