using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class ProductionLocationConfiguration : IEntityTypeConfiguration<ProductionLocation>
{
    public void Configure(EntityTypeBuilder<ProductionLocation> builder)
    {
        builder.HasKey(location => location.Id);
        builder.Property(location => location.Id).ValueGeneratedNever();
        builder.Property(location => location.Name).IsRequired().HasMaxLength(40);
        builder.Property(location => location.StationAccessKey).IsRequired().HasMaxLength(32);
        builder.Property(location => location.SlipLanguage).IsRequired().HasMaxLength(2);
        builder.Property(location => location.SortOrder).IsRequired();
        builder.Property(location => location.IsActive).IsRequired();
        builder.HasIndex(location => location.StationAccessKey).IsUnique();
    }
}
