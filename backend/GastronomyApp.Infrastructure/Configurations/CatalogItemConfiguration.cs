using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
  public void Configure(EntityTypeBuilder<CatalogItem> builder)
  {
    builder.HasKey(item => item.Id);
    builder.Property(item => item.Id).ValueGeneratedNever();
    builder.Property(item => item.Name).IsRequired().HasMaxLength(60);
    builder.Property(item => item.CategoryName).IsRequired().HasMaxLength(40);
    builder.Property(item => item.PriceCents).IsRequired();
    builder.Property(item => item.SortOrder).IsRequired();
    builder.Property(item => item.IsActive).IsRequired();
    builder.Property(item => item.IsAvailable).IsRequired();
  }
}
