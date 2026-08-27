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
        builder.Property(order => order.EventSessionId).IsRequired();
        builder.Property(order => order.ClientOrderId).IsRequired();
        builder.Property(order => order.GlobalOrderNumber).IsRequired();
        builder.Property(order => order.ServerPersonId).IsRequired();
        builder.Property(order => order.DeviceId).IsRequired();
        builder.Property(order => order.TableLabel).IsRequired().HasMaxLength(40);
        builder.Property(order => order.Note).IsRequired(false).HasMaxLength(200);
        builder.Property(order => order.TotalCents).IsRequired();
        builder.Property(order => order.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(order => order.CreatedAtUtc).IsRequired();
        builder.HasIndex(order => order.ClientOrderId).IsUnique();
        builder.HasMany(order => order.Lines).WithOne().HasForeignKey(line => line.OrderId);
        builder.HasMany(order => order.Tickets).WithOne().HasForeignKey(ticket => ticket.OrderId);
    }
}
