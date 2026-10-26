using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastronomyApp.Infrastructure.Configurations;

public sealed class OrderItemStatusConfiguration : IEntityTypeConfiguration<OrderItemStatus>
{
  public void Configure(EntityTypeBuilder<OrderItemStatus> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(table => table.HasCheckConstraint("CK_OrderItemStatuses_ExactlyOneCreator", "(\"CreatedByStaffMemberId\" IS NULL) <> (\"CreatedByStationId\" IS NULL)"));
    builder.HasKey(status => status.Id);
    builder.Property(status => status.Id).ValueGeneratedNever();
    builder.Property(status => status.OrderItemId).IsRequired();
    builder.Property(status => status.PreviousStatusId).IsRequired(false);
    builder.Property(status => status.Status).IsRequired();
    builder.Property(status => status.CreatedAtUtc).IsRequired();
    builder.Property(status => status.CreatedByStaffMemberId).IsRequired(false);
    builder.Property(status => status.CreatedByStationId).IsRequired(false);
    builder.HasIndex(status => status.OrderItemId);
    builder.HasIndex(status => status.PreviousStatusId).IsUnique();
    builder.HasOne(status => status.PreviousStatus).WithOne().HasForeignKey<OrderItemStatus>(status => status.PreviousStatusId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne(status => status.CreatedByStaffMember).WithMany().HasForeignKey(status => status.CreatedByStaffMemberId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne(status => status.CreatedByStation).WithMany().HasForeignKey(status => status.CreatedByStationId).OnDelete(DeleteBehavior.Restrict);
  }
}
