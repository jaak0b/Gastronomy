using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
  public void Configure(EntityTypeBuilder<Ingredient> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.HasKey(ingredient => ingredient.Id);
    builder.Property(ingredient => ingredient.Id).ValueGeneratedNever();
    builder.Property(ingredient => ingredient.Name).IsRequired().UseCollation("NOCASE");
    builder.Property(ingredient => ingredient.Unit).IsRequired();
    builder.Property(ingredient => ingredient.IsActive).IsRequired();
    builder.HasIndex(ingredient => ingredient.Name).IsUnique();
  }
}
