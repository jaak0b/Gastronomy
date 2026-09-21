using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class FestivalCatalogItemConfiguration : IEntityTypeConfiguration<FestivalCatalogItem>
{
  public void Configure(EntityTypeBuilder<FestivalCatalogItem> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.HasKey(menuRow => menuRow.Id);
    builder.Property(menuRow => menuRow.Id).ValueGeneratedNever();
    builder.Property(menuRow => menuRow.FestivalId).IsRequired();
    builder.Property(menuRow => menuRow.CatalogItemId).IsRequired();
    builder.Property(menuRow => menuRow.PriceCents).IsRequired();
    builder.Property(menuRow => menuRow.IsAvailable).IsRequired();
    builder.HasIndex(menuRow => new
                                {
                                  menuRow.FestivalId,
                                  menuRow.CatalogItemId
                                })
           .IsUnique();
    builder.HasOne(menuRow => menuRow.Festival).WithMany(festival => festival.CatalogItems).HasForeignKey(menuRow => menuRow.FestivalId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne(menuRow => menuRow.CatalogItem).WithMany(item => item.FestivalCatalogItems).HasForeignKey(menuRow => menuRow.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
  }
}
