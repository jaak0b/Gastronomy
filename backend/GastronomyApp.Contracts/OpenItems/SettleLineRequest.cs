namespace GastronomyApp.Contracts.OpenItems;

public sealed record SettleLineRequest
{
  public required Guid OrderItemId { get; init; }

  public required int? PaidPriceCents { get; init; }

  public string? PaymentNotice { get; init; }
}
