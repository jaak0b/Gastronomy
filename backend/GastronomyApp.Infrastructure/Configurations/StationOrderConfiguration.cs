using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class StationOrderConfiguration : IEntityTypeConfiguration<StationOrder>
{
  public void Configure(EntityTypeBuilder<StationOrder> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.HasKey(stationOrder => stationOrder.Id);
    builder.Property(stationOrder => stationOrder.Id).ValueGeneratedNever();
    builder.Property(stationOrder => stationOrder.OrderId).IsRequired();
    builder.Property(stationOrder => stationOrder.FestivalId).IsRequired();
    builder.Property(stationOrder => stationOrder.StationId).IsRequired();
    builder.Property(stationOrder => stationOrder.StationOrderNumber).IsRequired();
    builder.Property(stationOrder => stationOrder.DeliveryMode).IsRequired();
    builder.Property(stationOrder => stationOrder.IsHiddenFromAsItComesQueue).IsRequired();
    builder.HasIndex(stationOrder => new
                                     {
                                       stationOrder.OrderId,
                                       stationOrder.StationId
                                     })
           .IsUnique();
    builder.HasIndex(stationOrder => new
                                     {
                                       stationOrder.FestivalId,
                                       stationOrder.StationId,
                                       stationOrder.StationOrderNumber
                                     })
           .IsUnique();
    builder.HasOne(stationOrder => stationOrder.Festival).WithMany().HasForeignKey(stationOrder => stationOrder.FestivalId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne(stationOrder => stationOrder.Station).WithMany(station => station.StationOrders).HasForeignKey(stationOrder => stationOrder.StationId).OnDelete(DeleteBehavior.Restrict);
    builder.HasMany(stationOrder => stationOrder.Items).WithOne(item => item.StationOrder).HasForeignKey(item => item.StationOrderId);
  }
}
