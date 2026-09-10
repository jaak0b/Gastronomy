using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class ItemStationAssignmentConfiguration : IEntityTypeConfiguration<ItemStationAssignment>
{
  public void Configure(EntityTypeBuilder<ItemStationAssignment> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.HasKey(assignment => assignment.Id);
    builder.Property(assignment => assignment.Id).ValueGeneratedNever();
    builder.Property(assignment => assignment.FestivalId).IsRequired();
    builder.Property(assignment => assignment.CatalogItemId).IsRequired();
    builder.Property(assignment => assignment.StationId).IsRequired();
    builder.HasIndex(assignment => new
                                   {
                                     assignment.FestivalId,
                                     assignment.CatalogItemId,
                                     assignment.StationId
                                   }).IsUnique();
  }
}
