namespace GastronomyApp.Contracts.OpenItems;

public sealed record OpenOrderItemView(Guid OrderItemId, Guid OrderId, int GlobalOrderNumber, string ItemName, string? Note, int UnitPriceCents, DateTime OrderedAtUtc);
