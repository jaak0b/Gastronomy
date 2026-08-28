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
        builder.Property(item => item.ItemName).IsRequired().HasMaxLength(60);
        builder.Property(item => item.UnitPriceCents).IsRequired();
        builder.Property(item => item.Note).IsRequired(false).HasMaxLength(200);
    }
}
