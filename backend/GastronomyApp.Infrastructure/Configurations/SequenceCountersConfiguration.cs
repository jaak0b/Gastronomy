using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class SequenceCountersConfiguration : IEntityTypeConfiguration<SequenceCounters>
{
  public void Configure(EntityTypeBuilder<SequenceCounters> builder)
  {
    builder.HasKey(counters => counters.Id);
    builder.Property(counters => counters.Id).ValueGeneratedNever();
    builder.Property(counters => counters.NextOrderNumber).IsRequired();
    builder.Property(counters => counters.NextPrinterJobId).IsRequired();
  }
}
