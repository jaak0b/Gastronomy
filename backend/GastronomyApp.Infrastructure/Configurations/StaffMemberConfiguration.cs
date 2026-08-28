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
    builder.Property(staffMember => staffMember.CreatedAtUtc).IsRequired();
  }
}
