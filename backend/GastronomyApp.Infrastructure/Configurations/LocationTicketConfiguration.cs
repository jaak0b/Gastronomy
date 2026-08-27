using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class LocationTicketConfiguration : IEntityTypeConfiguration<LocationTicket>
{
    public void Configure(EntityTypeBuilder<LocationTicket> builder)
    {
        builder.HasKey(ticket => ticket.Id);
        builder.Property(ticket => ticket.Id).ValueGeneratedNever();
        builder.Property(ticket => ticket.OrderId).IsRequired();
        builder.Property(ticket => ticket.StationId).IsRequired();
        builder.Property(ticket => ticket.StationSequenceNumber).IsRequired();
        builder.Property(ticket => ticket.Status).IsRequired().HasConversion<string>().HasMaxLength(24);
        builder.Property(ticket => ticket.ReprintCount).IsRequired();
        builder.Property(ticket => ticket.CreatedAtUtc).IsRequired();
        builder.Property(ticket => ticket.ResolvedAtUtc).IsRequired(false);
        builder.Property(ticket => ticket.ResolutionNote).IsRequired(false).HasMaxLength(200);
        builder.HasIndex(ticket => new { ticket.OrderId, ticket.StationId }).IsUnique();
    }
}
