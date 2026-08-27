using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class ItemLocationAssignmentConfiguration : IEntityTypeConfiguration<ItemLocationAssignment>
{
    public void Configure(EntityTypeBuilder<ItemLocationAssignment> builder)
    {
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id).ValueGeneratedNever();
        builder.Property(assignment => assignment.CatalogItemId).IsRequired();
        builder.Property(assignment => assignment.ProductionLocationId).IsRequired();
        builder.HasIndex(assignment => new { assignment.CatalogItemId, assignment.ProductionLocationId }).IsUnique();
    }
}
