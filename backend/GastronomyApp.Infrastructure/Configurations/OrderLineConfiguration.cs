using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.Property(line => line.OrderId).IsRequired();
        builder.Property(line => line.LocationTicketId).IsRequired();
        builder.Property(line => line.CatalogItemId).IsRequired();
        builder.Property(line => line.ChosenStationId).IsRequired(false);
        builder.Property(line => line.ItemNameSnapshot).IsRequired().HasMaxLength(60);
        builder.Property(line => line.UnitPriceCentsSnapshot).IsRequired();
        builder.Property(line => line.Quantity).IsRequired();
        builder.Property(line => line.Note).IsRequired(false).HasMaxLength(100);
    }
}
