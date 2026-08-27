using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class StationConfiguration : IEntityTypeConfiguration<Station>
{
    public void Configure(EntityTypeBuilder<Station> builder)
    {
        builder.HasKey(station => station.Id);
        builder.Property(station => station.Id).ValueGeneratedNever();
        builder.Property(station => station.Name).IsRequired().HasMaxLength(40);
        builder.Property(station => station.SortOrder).IsRequired();
        builder.Property(station => station.IsActive).IsRequired();
    }
}
