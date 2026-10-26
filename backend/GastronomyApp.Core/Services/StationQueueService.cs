using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Stations;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class StationQueueService
{
  private readonly IGastronomyDatabase _database;
  private readonly TypeAdapterConfig _mapping;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly TimeProvider _timeProvider;

  public StationQueueService(IGastronomyDatabase database, TypeAdapterConfig mapping, RunningFestivalLookup runningFestival, TimeProvider timeProvider)
  {
    _database = database;
    _mapping = mapping;
    _runningFestival = runningFestival;
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

    List<StationOrder> fulfilled = await StationOrdersWithTheirCards(access.Value).Where(stationOrder => stationOrder.Items.Any(item => item.FulfilledAtUtc != null)).ToListAsync(cancellationToken);

    return new StationFulfilledView(fulfilled.Select(stationOrder => stationOrder.Adapt<StationOrderQueueView>(_mapping)).ToList());
  }

  public async Task<ErrorOr<StationQueueView>> FulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    ErrorOr<StationAtFestival> access = await FindStationAtFestivalAsync(stationId, cancellationToken);

    if (access.IsError)
      return access.Errors;

    List<Guid> selectedIds = orderItemIds.Distinct().ToList();
    Dictionary<Guid, OrderItem> itemsAtThisStation = await FindItemsAtStationAsync(selectedIds, stationId, cancellationToken);
    List<Error> refusedIds = selectedIds.Where(orderItemId => !itemsAtThisStation.ContainsKey(orderItemId)).Select(Refusal.StationQueue.UnknownOrderItemId).ToList();

    if (refusedIds.Count > 0)
      return refusedIds;

    var fulfilledAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

    foreach (var item in itemsAtThisStation.Values.Where(item => item.FulfilledAtUtc is null))
      item.FulfilledAtUtc = fulfilledAtUtc;

    await _database.SaveChangesAsync(cancellationToken);

    return await ReadQueueAtAsync(access.Value, cancellationToken);
  }

  public async Task<ErrorOr<StationQueueView>> UnfulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    ErrorOr<StationAtFestival> access = await FindStationAtFestivalAsync(stationId, cancellationToken);

    if (access.IsError)
      return access.Errors;

    List<Guid> selectedIds = orderItemIds.Distinct().ToList();
    Dictionary<Guid, OrderItem> itemsAtThisStation = await FindItemsAtStationAsync(selectedIds, stationId, cancellationToken);
    List<OrderItem> toPutBack = [];
    List<Error> refusedIds = [];

    foreach (var orderItemId in selectedIds)
    {
      if (!itemsAtThisStation.TryGetValue(orderItemId, out var item))
      {
        refusedIds.Add(Refusal.StationQueue.UnknownOrderItemId(orderItemId));
        continue;
      }

      if (item.FulfilledAtUtc is null)
      {
        refusedIds.Add(Refusal.StationQueue.ItemNotFulfilled(item.Id));
        continue;
      }

      toPutBack.Add(item);
    }

    if (refusedIds.Count > 0)
      return refusedIds;

    foreach (var item in toPutBack)
      item.FulfilledAtUtc = null;

    await _database.SaveChangesAsync(cancellationToken);

    return await ReadQueueAtAsync(access.Value, cancellationToken);
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

  private async Task<StationQueueView> ReadQueueAtAsync(StationAtFestival access, CancellationToken cancellationToken)
  {
    List<StationOrder> unfinished = await StationOrdersWithTheirCards(access).Where(stationOrder => stationOrder.Items.Any(item => item.FulfilledAtUtc == null)).ToListAsync(cancellationToken);
    List<StationOrderQueueView> orders = unfinished.Select(stationOrder => stationOrder.Adapt<StationOrderQueueView>(_mapping)).ToList();

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
    return await _database.OrderItems.Where(item => orderItemIds.Contains(item.Id) && item.StationOrder.StationId == stationId).ToDictionaryAsync(item => item.Id, cancellationToken);
  }

  private bool IsInAsItComesColumn(StationOrderQueueView stationOrder)
  {
    return stationOrder.DeliveryMode == DeliveryMode.AsItComes && !stationOrder.IsHiddenFromAsItComesQueue;
  }
}
