using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class NumberCounterConfiguration : IEntityTypeConfiguration<NumberCounter>
{
    public void Configure(EntityTypeBuilder<NumberCounter> builder)
    {
        builder.HasKey(counter => new
        {
            counter.CounterKind,
            counter.ProductionLocationId,
            counter.PrinterEndpointKey,
        });
        builder.Property(counter => counter.CounterKind).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(counter => counter.PrinterEndpointKey).HasMaxLength(96);
        builder.Property(counter => counter.NextValue).IsRequired();
    }
}
