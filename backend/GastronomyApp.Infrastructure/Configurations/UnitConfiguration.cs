using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
  public void Configure(EntityTypeBuilder<Unit> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.HasKey(unit => unit.Id);
    builder.Property(unit => unit.Id).ValueGeneratedNever();
    builder.Property(unit => unit.Name).IsRequired().UseCollation("NOCASE");
    builder.Property(unit => unit.IsActive).IsRequired();
    builder.HasIndex(unit => unit.Name).IsUnique();
  }
}
