using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record OrderStatusChangedEvent(Guid OrderId, OrderStatus Status);

public sealed record OrderItemsSettledEvent(
  IReadOnlyList<Guid> OrderItemIds,
  IReadOnlyList<string> TableNames);

public sealed record StationOrdersChangedEvent(Guid StationId);

public sealed record StationsChangedEvent;

public sealed record CatalogChangedEvent;

public sealed record EnrolmentCompletedEvent(
  DeviceOwnerKind DeviceKind,
  Guid OwnerId,
  string OwnerName,
  Guid DeviceId);

public sealed record DeviceRevokedEvent(Guid DeviceId);
