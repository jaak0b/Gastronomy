namespace GastronomyApp.Core.Requests;

public sealed record OrderSettlementLineTerms
{
  public required int? PaidPriceCents { get; init; }

  public string? PaymentNotice { get; init; }
}
