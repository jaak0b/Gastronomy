using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
  public void Configure(EntityTypeBuilder<Area> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.HasKey(area => area.Id);
    builder.Property(area => area.Id).ValueGeneratedNever();
    builder.Property(area => area.FestivalId).IsRequired();
    builder.Property(area => area.Name).IsRequired().UseCollation("NOCASE");
    builder.Property(area => area.SortOrder).IsRequired();
    builder.Property(area => area.IsHidden).IsRequired();
    builder.HasAlternateKey(area => new
                                    {
                                      area.Id,
                                      area.FestivalId
                                    });
    builder.HasIndex(area => new
                             {
                               area.FestivalId,
                               area.Name
                             })
           .IsUnique();
    builder.HasOne(area => area.Festival).WithMany(festival => festival.Areas).HasForeignKey(area => area.FestivalId).OnDelete(DeleteBehavior.Restrict);
  }
}
