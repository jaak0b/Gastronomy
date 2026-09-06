using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class StaffMemberConfiguration : IEntityTypeConfiguration<StaffMember>
{
  public void Configure(EntityTypeBuilder<StaffMember> builder)
  {
    builder.HasKey(staffMember => staffMember.Id);
    builder.Property(staffMember => staffMember.Id).ValueGeneratedNever();
    builder.Property(staffMember => staffMember.Name).IsRequired().HasMaxLength(40);
    builder.Property(staffMember => staffMember.IsActive).IsRequired();
    builder.Property(staffMember => staffMember.DeviceId).IsRequired(false);
    builder.Property(staffMember => staffMember.EnrolmentInvitationId).IsRequired(false);
    builder.Property(staffMember => staffMember.CreatedAtUtc).IsRequired();
    builder.HasIndex(staffMember => staffMember.DeviceId).IsUnique();
    builder.HasIndex(staffMember => staffMember.EnrolmentInvitationId).IsUnique();
    builder.HasOne<Device>()
           .WithOne()
           .HasForeignKey<StaffMember>(staffMember => staffMember.DeviceId)
           .OnDelete(DeleteBehavior.SetNull);
    builder.HasOne<EnrolmentInvitation>()
           .WithOne()
           .HasForeignKey<StaffMember>(staffMember => staffMember.EnrolmentInvitationId)
           .OnDelete(DeleteBehavior.SetNull);
  }
}
