namespace GastronomyApp.Core.Requests;

public sealed record OrderAcceptanceRequest
{
  public required Guid ClientOrderId { get; init; }

  public required Guid StaffMemberId { get; init; }

  public required string TableName { get; init; }

  public string? Note { get; init; }

  public required IReadOnlyList<OrderAcceptanceItemRequest> Items { get; init; }

  public IReadOnlyList<StationDeliveryModeRequest> DeliveryModes { get; init; } = [];
}
