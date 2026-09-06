using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Entities;

public sealed class StationOrder
{
  public required Guid Id { get; set; }

  public required Guid OrderId { get; set; }

  public required Guid StationId { get; set; }

  public required int StationOrderNumber { get; set; }

  public required DeliveryMode DeliveryMode { get; set; }

  public List<OrderItem> Items { get; set; } = [];
}
