namespace GastronomyApp.Contracts;

public sealed record OrderSettlementLineRequest
{
  public required int? PaidPriceCents { get; init; }

  public string? PaymentNotice { get; init; }
}
