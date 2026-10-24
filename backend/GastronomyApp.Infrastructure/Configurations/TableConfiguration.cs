using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class TableConfiguration : IEntityTypeConfiguration<Table>
{
  public void Configure(EntityTypeBuilder<Table> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.HasKey(table => table.Id);
    builder.Property(table => table.Id).ValueGeneratedNever();
    builder.Property(table => table.FestivalId).IsRequired();
    builder.Property(table => table.AreaId).IsRequired();
    builder.Property(table => table.Name).IsRequired().UseCollation("NOCASE");
    builder.Property(table => table.X).IsRequired();
    builder.Property(table => table.Y).IsRequired();
    builder.Property(table => table.Width).IsRequired();
    builder.Property(table => table.Height).IsRequired();
    builder.Property(table => table.Rotation).IsRequired();
    builder.Property(table => table.IsHidden).IsRequired();
    builder.HasIndex(table => new
                              {
                                table.FestivalId,
                                table.Name
                              })
           .IsUnique();
    builder.HasOne(table => table.Festival).WithMany().HasForeignKey(table => table.FestivalId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne(table => table.Area)
           .WithMany(area => area.Tables)
           .HasForeignKey(table => new
                                   {
                                     table.AreaId,
                                     table.FestivalId
                                   })
           .HasPrincipalKey(area => new
                                    {
                                      area.Id,
                                      area.FestivalId
                                    })
           .OnDelete(DeleteBehavior.Restrict);
  }
}
