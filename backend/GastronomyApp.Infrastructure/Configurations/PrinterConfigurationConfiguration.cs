using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class PrinterConfigurationConfiguration : IEntityTypeConfiguration<PrinterConfiguration>
{
    public void Configure(EntityTypeBuilder<PrinterConfiguration> builder)
    {
        builder.HasKey(configuration => configuration.StationId);
        builder.Property(configuration => configuration.StationId).ValueGeneratedNever();
        builder.Property(configuration => configuration.TransportKind).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(configuration => configuration.Host).IsRequired(false).HasMaxLength(64);
        builder.Property(configuration => configuration.Port).IsRequired();
        builder.Property(configuration => configuration.AgentIdentifier).IsRequired(false).HasMaxLength(64);
        builder.Property(configuration => configuration.CharactersPerLine).IsRequired();
        builder.Property(configuration => configuration.CodePageName).IsRequired().HasMaxLength(20);
        builder.Property(configuration => configuration.ConnectTimeoutSeconds).IsRequired();
        builder.Property(configuration => configuration.JobTimeoutSeconds).IsRequired();
        builder.Property(configuration => configuration.HeartbeatSeconds).IsRequired();
        builder.Property(configuration => configuration.IsEnabled).IsRequired();
    }
}
