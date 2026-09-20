using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class CatalogCategoryConfiguration : IEntityTypeConfiguration<CatalogCategory>
{
  private const int ColourHexLength = 7;

  public void Configure(EntityTypeBuilder<CatalogCategory> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.HasKey(category => category.Id);
    builder.Property(category => category.Id).ValueGeneratedNever();
    builder.Property(category => category.Name).IsRequired().UseCollation("NOCASE");
    builder.Property(category => category.ColourHex).IsRequired().HasMaxLength(ColourHexLength);
    builder.Property(category => category.SortOrder).IsRequired();
    builder.Property(category => category.IsActive).IsRequired();
    builder.HasIndex(category => category.Name).IsUnique();
  }
}
