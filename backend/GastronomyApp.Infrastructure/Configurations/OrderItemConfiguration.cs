using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
  public void Configure(EntityTypeBuilder<OrderItem> builder)
  {
    builder.HasKey(item => item.Id);
    builder.Property(item => item.Id).ValueGeneratedNever();
    builder.Property(item => item.StationOrderId).IsRequired();
    builder.Property(item => item.CatalogItemId).IsRequired();
    builder.Property(item => item.ItemName).IsRequired();
    builder.Property(item => item.UnitPriceCents).IsRequired();
    builder.Property(item => item.Note).IsRequired(false);
    builder.Property(item => item.ProductionStatus).IsRequired();
    builder.Property(item => item.SettledAtUtc).IsRequired(false);
    builder.Property(item => item.ChargedPriceCents).IsRequired(false);
    builder.Property(item => item.SettledByStaffMemberId).IsRequired(false);
    builder.Property(item => item.PaymentNotice).IsRequired(false);
    builder.HasIndex(item => item.SettledAtUtc);
    builder.HasIndex(item => item.ProductionStatus);
    builder.HasMany(item => item.StatusChanges)
           .WithOne()
           .HasForeignKey(change => change.OrderItemId);
  }
}
