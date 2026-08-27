using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class ServerPersonConfiguration : IEntityTypeConfiguration<ServerPerson>
{
    public void Configure(EntityTypeBuilder<ServerPerson> builder)
    {
        builder.HasKey(person => person.Id);
        builder.Property(person => person.Id).ValueGeneratedNever();
        builder.Property(person => person.Name).IsRequired().HasMaxLength(40);
        builder.Property(person => person.IsActive).IsRequired();
        builder.Property(person => person.CreatedAtUtc).IsRequired();
    }
}
