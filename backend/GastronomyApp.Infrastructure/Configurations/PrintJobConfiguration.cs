using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class PrintJobConfiguration : IEntityTypeConfiguration<PrintJob>
{
  public void Configure(EntityTypeBuilder<PrintJob> builder)
  {
    builder.HasKey(job => job.Id);
    builder.Property(job => job.Id).ValueGeneratedNever();
    builder.Property(job => job.StationOrderId).IsRequired();
    builder.Property(job => job.CopyNumber).IsRequired();
    builder.Property(job => job.Status).IsRequired();
    builder.Property(job => job.FailureReason).IsRequired(false);
    builder.Property(job => job.PrinterJobId).IsRequired(false);
    builder.Property(job => job.CreatedAtUtc).IsRequired();
    builder.Property(job => job.CompletedAtUtc).IsRequired(false);
    builder.HasIndex(job => new { job.StationOrderId, job.CopyNumber }).IsUnique();
  }
}
