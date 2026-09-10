using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class FestivalConfiguration : IEntityTypeConfiguration<Festival>
{
  public void Configure(EntityTypeBuilder<Festival> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.HasKey(festival => festival.Id);
    builder.Property(festival => festival.Id).ValueGeneratedNever();
    builder.Property(festival => festival.Name).IsRequired();
    builder.Property(festival => festival.StartsAtUtc)
           .IsRequired()
           .HasConversion(written => written, read => DateTime.SpecifyKind(read, DateTimeKind.Utc));
    builder.Property(festival => festival.EndsAtUtc)
           .IsRequired()
           .HasConversion(written => written, read => DateTime.SpecifyKind(read, DateTimeKind.Utc));
    builder.Property(festival => festival.NextOrderNumber).IsRequired().IsConcurrencyToken();
    builder.Property(festival => festival.IsHidden).IsRequired();
  }
}
