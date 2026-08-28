using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.HasKey(device => device.Id);
        builder.Property(device => device.Id).ValueGeneratedNever();
        builder.Property(device => device.StaffMemberId).IsRequired();
        builder.Property(device => device.Language).IsRequired().HasMaxLength(2);
        builder.Property(device => device.TokenHash).IsRequired();
        builder.Property(device => device.TokenSalt).IsRequired();
        builder.Property(device => device.TokenIterations).IsRequired();
        builder.Property(device => device.TokenAlgorithm).IsRequired().HasMaxLength(40);
        builder.Property(device => device.TokenLookupId).IsRequired().HasMaxLength(32);
        builder.Property(device => device.CreatedAtUtc).IsRequired();
        builder.Property(device => device.LastSeenAtUtc).IsRequired();
        builder.HasIndex(device => device.TokenLookupId).IsUnique();
        builder.HasIndex(device => device.StaffMemberId).IsUnique();
    }
}
