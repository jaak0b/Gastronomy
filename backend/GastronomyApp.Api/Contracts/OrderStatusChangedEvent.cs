using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record OrderStatusChangedEvent(Guid OrderId, OrderStatus Status);
