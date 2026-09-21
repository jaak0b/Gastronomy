namespace GastronomyApp.Contracts.Events;

public sealed record OrderItemsSettledEvent(IReadOnlyList<Guid> OrderItemIds, IReadOnlyList<string> TableNames);
