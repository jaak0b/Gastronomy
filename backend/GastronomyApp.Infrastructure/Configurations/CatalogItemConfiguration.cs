using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
  public void Configure(EntityTypeBuilder<CatalogItem> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.HasKey(item => item.Id);
    builder.Property(item => item.Id).ValueGeneratedNever();
    builder.Property(item => item.Name).IsRequired();
    builder.Property(item => item.CategoryId).IsRequired();
    builder.Property(item => item.SortOrder).IsRequired();
    builder.Property(item => item.IsActive).IsRequired();
    builder.Property(item => item.ProductionMinutes).IsRequired(false);
    builder.HasIndex(item => item.CategoryId);
    builder.HasOne<CatalogCategory>()
           .WithMany()
           .HasForeignKey(item => item.CategoryId)
           .OnDelete(DeleteBehavior.Restrict);
  }
}
