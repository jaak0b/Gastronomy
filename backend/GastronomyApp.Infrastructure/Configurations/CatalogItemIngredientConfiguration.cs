using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class CatalogItemIngredientConfiguration : IEntityTypeConfiguration<CatalogItemIngredient>
{
  public void Configure(EntityTypeBuilder<CatalogItemIngredient> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.HasKey(recipeLine => recipeLine.Id);
    builder.Property(recipeLine => recipeLine.Id).ValueGeneratedNever();
    builder.Property(recipeLine => recipeLine.CatalogItemId).IsRequired();
    builder.Property(recipeLine => recipeLine.IngredientId).IsRequired();
    builder.Property(recipeLine => recipeLine.Amount).IsRequired();
    builder.HasIndex(recipeLine => new
                                   {
                                     recipeLine.CatalogItemId,
                                     recipeLine.IngredientId
                                   })
           .IsUnique();
    builder.HasOne(recipeLine => recipeLine.CatalogItem).WithMany(item => item.Ingredients).HasForeignKey(recipeLine => recipeLine.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne(recipeLine => recipeLine.Ingredient).WithMany(ingredient => ingredient.CatalogItems).HasForeignKey(recipeLine => recipeLine.IngredientId).OnDelete(DeleteBehavior.Restrict);
  }
}
