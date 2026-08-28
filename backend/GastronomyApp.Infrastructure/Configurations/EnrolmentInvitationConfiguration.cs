using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class EnrolmentInvitationConfiguration : IEntityTypeConfiguration<EnrolmentInvitation>
{
    public const string OutstandingMarkerColumnName = "IsOutstandingMarker";

    public void Configure(EntityTypeBuilder<EnrolmentInvitation> builder)
    {
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Id).ValueGeneratedNever();
        builder.Property(invitation => invitation.StaffMemberId).IsRequired(false);
        builder.Property(invitation => invitation.QrCodeHash).IsRequired();
        builder.Property(invitation => invitation.QrCodeSalt).IsRequired();
        builder.Property(invitation => invitation.QrCodeIterations).IsRequired();
        builder.Property(invitation => invitation.QrCodeAlgorithm).IsRequired().HasMaxLength(40);
        builder.Property(invitation => invitation.CreatedAtUtc).IsRequired();
        builder.Property(invitation => invitation.ExpiresAtUtc).IsRequired();
        builder.Property(invitation => invitation.ConsumedAtUtc).IsRequired(false);
        builder.Property(invitation => invitation.ConsumedByDeviceId).IsRequired(false);

        builder.Property<int?>(OutstandingMarkerColumnName)
            .HasColumnType("INTEGER")
            .HasComputedColumnSql("CASE WHEN ConsumedAtUtc IS NULL THEN 1 END", stored: true);

        builder.HasIndex(OutstandingMarkerColumnName).IsUnique();
    }
}
