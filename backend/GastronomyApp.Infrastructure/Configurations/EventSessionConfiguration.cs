using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class EventSessionConfiguration : IEntityTypeConfiguration<EventSession>
{
    public void Configure(EntityTypeBuilder<EventSession> builder)
    {
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.Name).IsRequired().HasMaxLength(60);
        builder.Property(session => session.IsPractice).IsRequired();
        builder.Property(session => session.StartedAtUtc).IsRequired();
        builder.Property(session => session.EndedAtUtc).IsRequired(false);
        builder.Property(session => session.IsActive).IsRequired();
    }
}
