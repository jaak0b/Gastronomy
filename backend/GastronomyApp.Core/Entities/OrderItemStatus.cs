using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Announcements;

namespace GastronomyApp.Core.Entities;

[Raises(HubEvent.OrdersChanged)]
public sealed class OrderItemStatus : IEntity
{
  public required Guid Id { get; set; }

  public required Guid OrderItemId { get; set; }

  public Guid? PreviousStatusId { get; set; }

  public required OrderItemState Status { get; set; }

  public required DateTime CreatedAtUtc { get; set; }

  public Guid? CreatedByStaffMemberId { get; set; }

  public Guid? CreatedByStationId { get; set; }

  public OrderItem OrderItem { get; set; } = null!;

  public OrderItemStatus? PreviousStatus { get; set; }

  public StaffMember? CreatedByStaffMember { get; set; }

  public Station? CreatedByStation { get; set; }
}
