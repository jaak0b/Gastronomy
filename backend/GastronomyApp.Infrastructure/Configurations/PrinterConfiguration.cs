using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class PrinterConfiguration : IEntityTypeConfiguration<Printer>
{
  public void Configure(EntityTypeBuilder<Printer> builder)
  {
    builder.ToTable("Printers");
    builder.HasKey(printer => printer.Id);
    builder.Property(printer => printer.Id).ValueGeneratedNever();
    builder.Property(printer => printer.Name).IsRequired().HasMaxLength(40);
    builder.HasDiscriminator<string>("PrinterType")
           .HasValue<TestPrinter>("TestPrinter")
           .HasValue<EpsonTmT20ivNetworkPrinter>("EpsonTmT20ivNetworkPrinter");
  }
}

public sealed class EpsonTmT20ivNetworkPrinterConfiguration : IEntityTypeConfiguration<EpsonTmT20ivNetworkPrinter>
{
  public void Configure(EntityTypeBuilder<EpsonTmT20ivNetworkPrinter> builder)
  {
    builder.Property(printer => printer.Host).IsRequired().HasMaxLength(64);
    builder.Property(printer => printer.Port).IsRequired();
  }
}
