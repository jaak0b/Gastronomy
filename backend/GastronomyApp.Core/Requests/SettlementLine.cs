namespace GastronomyApp.Core.Requests;

public sealed record SettlementLine
{
  public required Guid OrderItemId { get; init; }

  public required int? PaidPriceCents { get; init; }

  public string? PaymentNotice { get; init; }
}
