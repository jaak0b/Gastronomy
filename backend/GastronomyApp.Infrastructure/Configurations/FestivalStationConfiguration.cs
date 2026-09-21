using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class FestivalStationConfiguration : IEntityTypeConfiguration<FestivalStation>
{
  public void Configure(EntityTypeBuilder<FestivalStation> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.HasKey(link => link.Id);
    builder.Property(link => link.Id).ValueGeneratedNever();
    builder.Property(link => link.FestivalId).IsRequired();
    builder.Property(link => link.StationId).IsRequired();
    builder.Property(link => link.NextStationOrderNumber).IsRequired().IsConcurrencyToken();
    builder.HasIndex(link => new
                             {
                               link.FestivalId,
                               link.StationId
                             })
           .IsUnique();
    builder.HasOne(link => link.Festival).WithMany(festival => festival.Stations).HasForeignKey(link => link.FestivalId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne(link => link.Station).WithMany(station => station.FestivalStations).HasForeignKey(link => link.StationId).OnDelete(DeleteBehavior.Restrict);
  }
}
