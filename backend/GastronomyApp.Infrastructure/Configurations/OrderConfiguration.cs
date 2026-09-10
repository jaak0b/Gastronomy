using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
  public void Configure(EntityTypeBuilder<Order> builder)
  {
    builder.HasKey(order => order.Id);
    builder.Property(order => order.Id).ValueGeneratedNever();
    builder.Property(order => order.ClientOrderId).IsRequired();
    builder.Property(order => order.FestivalId).IsRequired();
    builder.Property(order => order.GlobalOrderNumber).IsRequired();
    builder.Property(order => order.StaffMemberId).IsRequired();
    builder.Property(order => order.TableName).IsRequired();
    builder.Property(order => order.Note).IsRequired(false);
    builder.Property(order => order.CreatedAtUtc).IsRequired();
    builder.HasIndex(order => order.ClientOrderId).IsUnique();
    builder.HasIndex(order => order.FestivalId);
    builder.HasIndex(order => new { order.FestivalId, order.GlobalOrderNumber }).IsUnique();
    builder.HasOne<Festival>()
           .WithMany()
           .HasForeignKey(order => order.FestivalId)
           .OnDelete(DeleteBehavior.Restrict);
    builder.HasMany(order => order.StationOrders)
           .WithOne()
           .HasForeignKey(stationOrder => stationOrder.OrderId);
  }
}
