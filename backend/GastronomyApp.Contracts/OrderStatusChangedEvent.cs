using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts;

public sealed record OrderStatusChangedEvent(Guid OrderId, OrderStatus Status);
