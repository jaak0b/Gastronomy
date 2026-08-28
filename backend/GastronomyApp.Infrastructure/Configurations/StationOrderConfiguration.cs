using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class StationOrderConfiguration : IEntityTypeConfiguration<StationOrder>
{
  public void Configure(EntityTypeBuilder<StationOrder> builder)
  {
    builder.HasKey(stationOrder => stationOrder.Id);
    builder.Property(stationOrder => stationOrder.Id).ValueGeneratedNever();
    builder.Property(stationOrder => stationOrder.OrderId).IsRequired();
    builder.Property(stationOrder => stationOrder.StationId).IsRequired();
    builder.Property(stationOrder => stationOrder.StationOrderNumber).IsRequired();
    builder.HasIndex(stationOrder => new { stationOrder.OrderId, stationOrder.StationId }).IsUnique();
    builder.HasOne<Station>()
        .WithMany()
        .HasForeignKey(stationOrder => stationOrder.StationId)
        .OnDelete(DeleteBehavior.Restrict);
    builder.HasMany(stationOrder => stationOrder.Items)
        .WithOne()
        .HasForeignKey(item => item.StationOrderId);
    builder.HasMany(stationOrder => stationOrder.PrintJobs)
        .WithOne()
        .HasForeignKey(job => job.StationOrderId);
  }
}
