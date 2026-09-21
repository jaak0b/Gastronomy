using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.Events;

public sealed record OrderStatusChangedEvent(Guid OrderId, OrderStatus Status);
