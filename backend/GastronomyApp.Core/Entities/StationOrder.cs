using System.Collections.ObjectModel;
using GastronomyApp.Contracts.Enums;

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
}
