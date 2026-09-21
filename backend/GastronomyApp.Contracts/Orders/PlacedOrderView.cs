using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.Orders;

public sealed record PlacedOrderView(Guid OrderId, int GlobalOrderNumber, OrderStatus Status, int TotalCents, DateTime CreatedAtUtc, IReadOnlyList<StationOrderView> StationOrders);
