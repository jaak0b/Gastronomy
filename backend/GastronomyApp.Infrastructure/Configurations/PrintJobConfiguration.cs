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
        builder.Property(job => job.LocationTicketId).IsRequired(false);
        builder.Property(job => job.ProductionLocationId).IsRequired();
        builder.Property(job => job.Kind).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(job => job.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(job => job.ProcessId).IsRequired(false);
        builder.Property(job => job.FailureReason).IsRequired(false).HasConversion<string>().HasMaxLength(24);
        builder.Property(job => job.RequestedByDeviceId).IsRequired(false);
        builder.Property(job => job.CreatedAtUtc).IsRequired();
        builder.Property(job => job.CompletedAtUtc).IsRequired(false);
    }
}
