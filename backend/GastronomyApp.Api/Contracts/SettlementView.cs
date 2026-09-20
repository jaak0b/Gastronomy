namespace GastronomyApp.Api.Contracts;

public sealed record SettlementView(
  IReadOnlyList<Guid> SettledOrderItemIds,
  IReadOnlyList<Guid> ReappliedOrderItemIds,
  IReadOnlyList<Guid> AlreadySettledByOthersOrderItemIds,
  bool OtherPhonesWereTold);
