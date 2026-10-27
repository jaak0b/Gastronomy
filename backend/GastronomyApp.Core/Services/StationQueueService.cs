using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Stations;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class StationQueueService
{
  private readonly IGastronomyDatabase _database;
  private readonly OrderItemLifecycle _itemLifecycle;
  private readonly TypeAdapterConfig _mapping;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly TimeProvider _timeProvider;

  public StationQueueService(IGastronomyDatabase database, TypeAdapterConfig mapping, RunningFestivalLookup runningFestival, OrderItemLifecycle itemLifecycle, TimeProvider timeProvider)
  {
    _database = database;
    _mapping = mapping;
    _runningFestival = runningFestival;
    _itemLifecycle = itemLifecycle;
    _timeProvider = timeProvider;
  }

  public async Task<ErrorOr<StationQueueView>> ReadQueueAsync(Guid stationId, CancellationToken cancellationToken)
  {
    ErrorOr<StationAtFestival> access = await FindStationAtFestivalAsync(stationId, cancellationToken);

    if (access.IsError)
      return access.Errors;

    return await ReadQueueAtAsync(access.Value, cancellationToken);
  }

  public async Task<ErrorOr<StationFulfilledView>> ReadFulfilledAsync(Guid stationId, CancellationToken cancellationToken)
  {
    ErrorOr<StationAtFestival> access = await FindStationAtFestivalAsync(stationId, cancellationToken);

    if (access.IsError)
      return access.Errors;

    List<StationOrder> fulfilled = await StationOrdersWithTheirCards(access.Value).WhereListedAsDone().ToListAsync(cancellationToken);

    return new StationFulfilledView(fulfilled.Select(stationOrder => stationOrder.Adapt<StationOrderQueueView>(_mapping)).ToList());
  }

  public async Task<ErrorOr<StationQueueView>> FulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    return await AppendStatusToSelectedItemsAsync(orderItemIds, stationId, OrderItemState.HandedOut, KeepTheFirstHandOut, cancellationToken);
  }

  public async Task<ErrorOr<StationQueueView>> UnfulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    return await AppendStatusToSelectedItemsAsync(orderItemIds, stationId, OrderItemState.Open, RefuseThePutBack, cancellationToken);
  }

  public async Task<ErrorOr<StationQueueView>> AcknowledgeCancellationAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    return await AppendStatusToSelectedItemsAsync(orderItemIds, stationId, OrderItemState.CancellationAcknowledged, KeepTheFirstAcknowledgement, cancellationToken);
  }

  public async Task<ErrorOr<StationQueueView>> HideFromAsItComesQueueAsync(Guid stationOrderId, Guid stationId, CancellationToken cancellationToken)
  {
    ErrorOr<StationAtFestival> access = await FindStationAtFestivalAsync(stationId, cancellationToken);

    if (access.IsError)
      return access.Errors;

    var stationOrder = await _database.StationOrders.FirstOrDefaultAsync(candidate => candidate.Id == stationOrderId && candidate.StationId == stationId && candidate.FestivalId == access.Value.FestivalId, cancellationToken);

    if (stationOrder is null)
      return Refusal.StationQueue.OrderNotAtThisStation(stationOrderId);

    if (stationOrder.DeliveryMode != DeliveryMode.AsItComes)
      return Refusal.StationQueue.NotAnAsItComesOrder(stationOrder.Id);

    stationOrder.IsHiddenFromAsItComesQueue = true;

    await _database.SaveChangesAsync(cancellationToken);

    return await ReadQueueAtAsync(access.Value, cancellationToken);
  }

  private async Task<ErrorOr<StationAtFestival>> FindStationAtFestivalAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var station = await _database.Stations.AsNoTracking().Where(candidate => candidate.Id == stationId).ProjectToType<StationSummaryView>(_mapping).FirstOrDefaultAsync(cancellationToken);

    if (station is null)
      return Refusal.StationQueue.StationUnknown();

    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return Refusal.StationQueue.NoRunningFestival();

    if (!await _database.FestivalStations.AsNoTracking().AnyAsync(link => link.FestivalId == festival.Id && link.StationId == stationId, cancellationToken))
      return Refusal.StationQueue.StationNotAtTheFestival();

    return new StationAtFestival(station, festival.Id);
  }

  private async Task<ErrorOr<StationQueueView>> AppendStatusToSelectedItemsAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, OrderItemState targetState, Func<OrderItem, OrderItemStatusChange, ErrorOr<Success>> acceptTheRepeat, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    ErrorOr<StationAtFestival> access = await FindStationAtFestivalAsync(stationId, cancellationToken);

    if (access.IsError)
      return access.Errors;

    List<Guid> selectedIds = orderItemIds.Distinct().ToList();
    Dictionary<Guid, OrderItem> itemsAtThisStation = await FindItemsAtStationAsync(selectedIds, stationId, cancellationToken);
    List<OrderItem> toChange = [];
    List<Error> refusedIds = [];

    foreach (var orderItemId in selectedIds)
    {
      if (!itemsAtThisStation.TryGetValue(orderItemId, out var item))
      {
        refusedIds.Add(Refusal.StationQueue.UnknownOrderItemId(orderItemId));
        continue;
      }

      var change = _itemLifecycle.CheckStatusChange(item, targetState);

      if (change == OrderItemStatusChange.Allowed)
      {
        toChange.Add(item);
        continue;
      }

      if (change == OrderItemStatusChange.ItemIsCancelled)
      {
        refusedIds.Add(Refusal.StationQueue.ItemWasCancelled(item.Id, item.ItemName));
        continue;
      }

      ErrorOr<Success> accepted = acceptTheRepeat(item, change);

      if (accepted.IsError)
        refusedIds.AddRange(accepted.Errors);
    }

    if (refusedIds.Count > 0)
      return refusedIds;

    var changedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
    OrderItemStatusAuthor station = new(null, stationId);

    foreach (var item in toChange)
      _itemLifecycle.AppendStatus(item, targetState, station, changedAtUtc);

    await _database.SaveChangesAsync(cancellationToken);

    return await ReadQueueAtAsync(access.Value, cancellationToken);
  }

  private ErrorOr<Success> KeepTheFirstHandOut(OrderItem item, OrderItemStatusChange change)
  {
    if (change == OrderItemStatusChange.AlreadyInThatState)
      return Result.Success;

    throw new InvalidOperationException($"Order item {item.Id} could not be handed out ({change}), although every item that is neither handed out nor cancelled may be.");
  }

  private ErrorOr<Success> RefuseThePutBack(OrderItem item, OrderItemStatusChange change)
  {
    return Refusal.StationQueue.ItemNotFulfilled(item.Id);
  }

  private ErrorOr<Success> KeepTheFirstAcknowledgement(OrderItem item, OrderItemStatusChange change)
  {
    if (change == OrderItemStatusChange.AlreadyInThatState)
      return Result.Success;

    return Refusal.StationQueue.ItemNotAwaitingCancellationAcknowledgement(item.Id);
  }

  private async Task<StationQueueView> ReadQueueAtAsync(StationAtFestival access, CancellationToken cancellationToken)
  {
    List<StationOrder> unfinished = await StationOrdersWithTheirCards(access).Include(stationOrder => stationOrder.Items).ThenInclude(item => item.CurrentStatus!.PreviousStatus).WhereAnItemIsOpenOrAwaitsCancellationAcknowledgement().ToListAsync(cancellationToken);
    List<StationOrderQueueView> orders = unfinished.Select(QueueCardOf).ToList();

    return new(access.Station, orders, orders.Where(IsInAsItComesColumn).ToList());
  }

  private IOrderedQueryable<StationOrder> StationOrdersWithTheirCards(StationAtFestival access)
  {
    return _database.StationOrders.AsNoTracking()
                    .Where(stationOrder => stationOrder.StationId == access.Station.Id && stationOrder.FestivalId == access.FestivalId)
                    .Include(stationOrder => stationOrder.Items)
                    .Include(stationOrder => stationOrder.Order)
                    .ThenInclude(order => order.StaffMember)
                    .Include(stationOrder => stationOrder.Order)
                    .ThenInclude(order => order.Table)
                    .OrderBy(stationOrder => stationOrder.StationOrderNumber);
  }

  private async Task<Dictionary<Guid, OrderItem>> FindItemsAtStationAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    return await _database.OrderItems.Include(item => item.CurrentStatus!.PreviousStatus).Where(item => orderItemIds.Contains(item.Id) && item.StationOrder.StationId == stationId).ToDictionaryAsync(item => item.Id, cancellationToken);
  }

  private StationOrderQueueView QueueCardOf(StationOrder stationOrder)
  {
    var card = stationOrder.Adapt<StationOrderQueueView>(_mapping);
    HashSet<Guid> itemIdsOnTheCard = stationOrder.Items.Where(item => item.IsOnTheQueueCard()).Select(item => item.Id).ToHashSet();

    return card with { Items = card.Items.Where(item => itemIdsOnTheCard.Contains(item.OrderItemId)).ToList() };
  }

  private bool IsInAsItComesColumn(StationOrderQueueView stationOrder)
  {
    return stationOrder.DeliveryMode == DeliveryMode.AsItComes && !stationOrder.IsHiddenFromAsItComesQueue;
  }
}
