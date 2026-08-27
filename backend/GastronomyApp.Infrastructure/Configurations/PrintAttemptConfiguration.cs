using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class PrintAttemptConfiguration : IEntityTypeConfiguration<PrintAttempt>
{
    public void Configure(EntityTypeBuilder<PrintAttempt> builder)
    {
        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.Id).ValueGeneratedNever();
        builder.Property(attempt => attempt.PrintJobId).IsRequired();
        builder.Property(attempt => attempt.AttemptNumber).IsRequired();
        builder.Property(attempt => attempt.Outcome).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(attempt => attempt.Phase).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(attempt => attempt.BytesWritten).IsRequired();
        builder.Property(attempt => attempt.TransportDetail).IsRequired().HasMaxLength(400);
        builder.Property(attempt => attempt.PrinterStatusSnapshotJson).IsRequired();
        builder.Property(attempt => attempt.StartedAtUtc).IsRequired();
        builder.Property(attempt => attempt.EndedAtUtc).IsRequired();
    }
}
