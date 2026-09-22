using System.Collections.ObjectModel;
using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Entities;

public sealed class StationOrder
{
  public required Guid Id { get; set; }

  public required Guid OrderId { get; set; }

  public required Guid FestivalId { get; set; }

  public required Guid StationId { get; set; }

  public required int StationOrderNumber { get; set; }

  public required DeliveryMode DeliveryMode { get; set; }

  public bool IsHiddenFromAsItComesQueue { get; set; }

  public Order Order { get; set; } = null!;

  public Festival Festival { get; set; } = null!;

  public Station Station { get; set; } = null!;

  public Collection<OrderItem> Items { get; } = [];

  public bool IsInAsItComesColumn()
  {
    return DeliveryMode == DeliveryMode.AsItComes && !IsHiddenFromAsItComesQueue;
  }

  public ErrorOr<StationOrder> HideFromAsItComesQueue()
  {
    if (DeliveryMode != DeliveryMode.AsItComes)
      return Refusal.StationQueue.NotAnAsItComesOrder(Id);

    IsHiddenFromAsItComesQueue = true;

    return this;
  }
}
