using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record PlacedOrderView(Guid OrderId, int GlobalOrderNumber, OrderStatus Status, int TotalCents, DateTime CreatedAtUtc, IReadOnlyList<StationOrderView> StationOrders);
