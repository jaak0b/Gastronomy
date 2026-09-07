using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class StationConfiguration : IEntityTypeConfiguration<Station>
{
  public void Configure(EntityTypeBuilder<Station> builder)
  {
    builder.HasKey(station => station.Id);
    builder.Property(station => station.Id).ValueGeneratedNever();
    builder.Property(station => station.Name).IsRequired();
    builder.Property(station => station.SortOrder).IsRequired();
    builder.Property(station => station.IsActive).IsRequired();
    builder.Property(station => station.DeviceId).IsRequired(false);
    builder.Property(station => station.EnrolmentInvitationId).IsRequired(false);
    builder.Property(station => station.NextStationOrderNumber).IsRequired();
    builder.HasIndex(station => station.DeviceId).IsUnique();
    builder.HasIndex(station => station.EnrolmentInvitationId).IsUnique();
    builder.HasOne<Device>()
           .WithOne()
           .HasForeignKey<Station>(station => station.DeviceId)
           .OnDelete(DeleteBehavior.SetNull);
    builder.HasOne<EnrolmentInvitation>()
           .WithOne()
           .HasForeignKey<Station>(station => station.EnrolmentInvitationId)
           .OnDelete(DeleteBehavior.SetNull);
  }
}
