using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class PrinterStatusConfiguration : IEntityTypeConfiguration<PrinterStatus>
{
  public void Configure(EntityTypeBuilder<PrinterStatus> builder)
  {
    builder.HasKey(status => status.PrinterId);
    builder.Property(status => status.PrinterId).ValueGeneratedNever();
    builder.Property(status => status.IsOnline).IsRequired();
    builder.Property(status => status.IsPaperEnd).IsRequired();
    builder.Property(status => status.IsPaperNearEnd).IsRequired();
    builder.Property(status => status.IsCoverOpen).IsRequired();
    builder.Property(status => status.IsInErrorState).IsRequired();
    builder.Property(status => status.IsFaulty).IsRequired();
    builder.Property(status => status.LastDetail).IsRequired().HasMaxLength(200);
    builder.Property(status => status.LastChangedAtUtc).IsRequired();
    builder.Property(status => status.LastHeardFromAtUtc).IsRequired();
  }
}
