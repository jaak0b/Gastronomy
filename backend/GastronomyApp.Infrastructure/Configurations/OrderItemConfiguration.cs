using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
  public void Configure(EntityTypeBuilder<OrderItem> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.HasKey(item => item.Id);
    builder.Property(item => item.Id).ValueGeneratedNever();
    builder.Property(item => item.StationOrderId).IsRequired();
    builder.Property(item => item.CatalogItemId).IsRequired();
    builder.Property(item => item.ItemName).IsRequired();
    builder.Property(item => item.UnitPriceCents).IsRequired();
    builder.Property(item => item.Note).IsRequired(false);
    builder.Property(item => item.CurrentStatusId).IsRequired(false);
    builder.Property(item => item.SettledAtUtc).IsRequired(false);
    builder.Property(item => item.ChargedPriceCents).IsRequired(false);
    builder.Property(item => item.SettledByStaffMemberId).IsRequired(false);
    builder.Property(item => item.PaymentNotice).IsRequired(false);
    builder.Property(item => item.PaymentMethod).IsRequired(false);
    builder.HasIndex(item => item.SettledAtUtc);
    builder.HasIndex(item => item.CurrentStatusId).IsUnique();
    builder.HasOne(item => item.CatalogItem).WithMany().HasForeignKey(item => item.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne(item => item.SettledByStaffMember).WithMany().HasForeignKey(item => item.SettledByStaffMemberId).OnDelete(DeleteBehavior.Restrict);
    builder.HasMany(item => item.Statuses).WithOne(status => status.OrderItem).HasForeignKey(status => status.OrderItemId).OnDelete(DeleteBehavior.Cascade);
    builder.HasOne(item => item.CurrentStatus).WithOne().HasForeignKey<OrderItem>(item => item.CurrentStatusId).OnDelete(DeleteBehavior.NoAction);
  }
}
