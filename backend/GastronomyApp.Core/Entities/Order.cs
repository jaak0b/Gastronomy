namespace GastronomyApp.Core.Entities;

public sealed class Order
{
  public required Guid Id { get; set; }

  public required Guid ClientOrderId { get; set; }

  public required int GlobalOrderNumber { get; set; }

  public required Guid StaffMemberId { get; set; }

  public required string TableName { get; set; }

  public string? Note { get; set; }

  public required DateTime CreatedAtUtc { get; set; }

  public List<StationOrder> StationOrders { get; set; } = [];
}
