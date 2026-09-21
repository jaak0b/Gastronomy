using System.Collections.ObjectModel;
using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Core.Entities;

public sealed class Order
{
  public required Guid Id { get; set; }

  public required Guid ClientOrderId { get; set; }

  public required Guid FestivalId { get; set; }

  public required int GlobalOrderNumber { get; set; }

  public required Guid StaffMemberId { get; set; }

  public required string TableName { get; set; }

  public required DateTime CreatedAtUtc { get; set; }

  public Festival Festival { get; set; } = null!;

  public StaffMember StaffMember { get; set; } = null!;

  public Collection<StationOrder> StationOrders { get; } = [];

  public OrderStatus Status()
  {
    List<OrderItem> items = StationOrders.SelectMany(stationOrder => stationOrder.Items).ToList();

    var fulfilledItemCount = items.Count(item => item.FulfilledAtUtc is not null);

    if (fulfilledItemCount == 0)
      return OrderStatus.Open;

    if (fulfilledItemCount >= items.Count)
      return OrderStatus.Fulfilled;

    return OrderStatus.PartiallyFulfilled;
  }

  public int TotalCents()
  {
    return StationOrders.SelectMany(stationOrder => stationOrder.Items).Sum(item => item.UnitPriceCents);
  }
}
