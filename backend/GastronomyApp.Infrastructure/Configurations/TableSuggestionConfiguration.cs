using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class TableSuggestionConfiguration : IEntityTypeConfiguration<TableSuggestion>
{
    public void Configure(EntityTypeBuilder<TableSuggestion> builder)
    {
        builder.HasKey(suggestion => suggestion.Id);
        builder.Property(suggestion => suggestion.Id).ValueGeneratedNever();
        builder.Property(suggestion => suggestion.Label).IsRequired().HasMaxLength(40);
        builder.Property(suggestion => suggestion.SortOrder).IsRequired();
    }
}
