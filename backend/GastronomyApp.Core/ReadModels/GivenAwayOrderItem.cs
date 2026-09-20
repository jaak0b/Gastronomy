namespace GastronomyApp.Core.ReadModels;

public sealed record GivenAwayOrderItem
{
  public required Guid OrderItemId { get; init; }

  public required Guid OrderId { get; init; }

  public required int GlobalOrderNumber { get; init; }

  public required string ItemName { get; init; }

  public required int WaivedAmountCents { get; init; }

  public required string? PaymentNotice { get; init; }

  public required DateTime SettledAtUtc { get; init; }
}
