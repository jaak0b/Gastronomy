using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class FestivalIngredientConfiguration : IEntityTypeConfiguration<FestivalIngredient>
{
  public void Configure(EntityTypeBuilder<FestivalIngredient> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.HasKey(stock => stock.Id);
    builder.Property(stock => stock.Id).ValueGeneratedNever();
    builder.Property(stock => stock.FestivalId).IsRequired();
    builder.Property(stock => stock.IngredientId).IsRequired();
    builder.Property(stock => stock.AvailableAmount).IsRequired(false);
    builder.HasIndex(stock => new
                              {
                                stock.FestivalId,
                                stock.IngredientId
                              })
           .IsUnique();
    builder.HasOne(stock => stock.Festival).WithMany(festival => festival.Ingredients).HasForeignKey(stock => stock.FestivalId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne(stock => stock.Ingredient).WithMany(ingredient => ingredient.FestivalIngredients).HasForeignKey(stock => stock.IngredientId).OnDelete(DeleteBehavior.Restrict);
  }
}
