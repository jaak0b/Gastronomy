namespace GastronomyApp.Contracts.OpenItems;

public sealed record SettlementView(IReadOnlyList<Guid> SettledOrderItemIds, IReadOnlyList<Guid> ReappliedOrderItemIds, IReadOnlyList<Guid> AlreadySettledByOthersOrderItemIds);
