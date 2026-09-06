using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class OrderItemStatusChangeConfiguration : IEntityTypeConfiguration<OrderItemStatusChange>
{
  public void Configure(EntityTypeBuilder<OrderItemStatusChange> builder)
  {
    builder.HasKey(change => change.Id);
    builder.Property(change => change.Id).ValueGeneratedNever();
    builder.Property(change => change.OrderItemId).IsRequired();
    builder.Property(change => change.Status).IsRequired();
    builder.Property(change => change.ChangedAtUtc).IsRequired();
    builder.HasIndex(change => change.OrderItemId);
  }
}
