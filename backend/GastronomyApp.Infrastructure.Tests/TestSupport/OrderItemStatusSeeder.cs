using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class OrderItemStatusSeeder
{
  private readonly OrderItemLifecycle _lifecycle = new();
  private readonly List<PendingHandOut> _pendingHandOuts = [];
  private readonly List<OrderItem> _waitingForTheirFirstStatus = [];

  public OrderItem Open(OrderItem item, Guid staffMemberId, DateTime placedAtUtc)
  {
    _lifecycle.AppendStatus(item, OrderItemState.Open, new(staffMemberId, null), placedAtUtc);
    _waitingForTheirFirstStatus.Add(item);

    return item;
  }

  public OrderItem OpenAndHandOutOnSaveWhenAHandOutTimeIsGiven(OrderItem item, Guid staffMemberId, DateTime placedAtUtc, Guid stationId, DateTime? handedOutAtUtc)
  {
    Open(item, staffMemberId, placedAtUtc);

    if (handedOutAtUtc is { } handedOut)
      _pendingHandOuts.Add(new(item, stationId, handedOut));

    return item;
  }

  public OrderItem OpenAndMakeCurrent(OrderItem item, Guid staffMemberId, DateTime placedAtUtc)
  {
    _lifecycle.AppendStatus(item, OrderItemState.Open, new(staffMemberId, null), placedAtUtc);
    _lifecycle.PointAtFirstStatus(item);

    return item;
  }

  public OrderItem HandOut(OrderItem item, Guid stationId, DateTime handedOutAtUtc)
  {
    _lifecycle.AppendStatus(item, OrderItemState.HandedOut, new(null, stationId), handedOutAtUtc);

    return item;
  }

  public OrderItem PutBack(OrderItem item, Guid stationId, DateTime putBackAtUtc)
  {
    _lifecycle.AppendStatus(item, OrderItemState.Open, new(null, stationId), putBackAtUtc);

    return item;
  }

  public OrderItem Cancel(OrderItem item, Guid staffMemberId, DateTime cancelledAtUtc)
  {
    _lifecycle.AppendStatus(item, OrderItemState.Cancelled, new(staffMemberId, null), cancelledAtUtc);

    return item;
  }

  public OrderItem AcknowledgeCancellation(OrderItem item, Guid stationId, DateTime acknowledgedAtUtc)
  {
    _lifecycle.AppendStatus(item, OrderItemState.CancellationAcknowledged, new(null, stationId), acknowledgedAtUtc);

    return item;
  }

  public async Task SaveAsync(IGastronomyDatabase database, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(database);

    await database.SaveChangesAsync(cancellationToken);

    foreach (var item in _waitingForTheirFirstStatus)
      _lifecycle.PointAtFirstStatus(item);

    _waitingForTheirFirstStatus.Clear();
    await database.SaveChangesAsync(cancellationToken);

    if (_pendingHandOuts.Count == 0)
      return;

    foreach (var handOut in _pendingHandOuts)
      HandOut(handOut.Item, handOut.StationId, handOut.HandedOutAtUtc);

    _pendingHandOuts.Clear();
    await database.SaveChangesAsync(cancellationToken);
  }
}
