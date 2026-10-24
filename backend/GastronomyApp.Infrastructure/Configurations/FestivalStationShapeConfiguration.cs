using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class FestivalStationShapeConfiguration : IEntityTypeConfiguration<FestivalStationShape>
{
  public void Configure(EntityTypeBuilder<FestivalStationShape> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.HasKey(shape => shape.FestivalStationId);
    builder.Property(shape => shape.FestivalStationId).ValueGeneratedNever();
    builder.Property(shape => shape.FestivalId).IsRequired();
    builder.Property(shape => shape.AreaId).IsRequired();
    builder.Property(shape => shape.X).IsRequired();
    builder.Property(shape => shape.Y).IsRequired();
    builder.Property(shape => shape.Width).IsRequired();
    builder.Property(shape => shape.Height).IsRequired();
    builder.Property(shape => shape.Rotation).IsRequired();
    builder.HasOne(shape => shape.Festival).WithMany().HasForeignKey(shape => shape.FestivalId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne(shape => shape.Area)
           .WithMany(area => area.StationShapes)
           .HasForeignKey(shape => new
                                   {
                                     shape.AreaId,
                                     shape.FestivalId
                                   })
           .HasPrincipalKey(area => new
                                    {
                                      area.Id,
                                      area.FestivalId
                                    })
           .OnDelete(DeleteBehavior.Restrict);
    builder.HasOne(shape => shape.FestivalStation)
           .WithOne()
           .HasForeignKey<FestivalStationShape>(shape => new
                                                         {
                                                           shape.FestivalStationId,
                                                           shape.FestivalId
                                                         })
           .HasPrincipalKey<FestivalStation>(link => new
                                                     {
                                                       link.Id,
                                                       link.FestivalId
                                                     })
           .OnDelete(DeleteBehavior.Restrict);
  }
}
