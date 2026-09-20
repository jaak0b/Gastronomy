using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public sealed record QueuedItemRow(Guid StationId, double? ProductionMinutes, bool IsQueueIndependent);

public sealed class StationsAtTheFestivalReader
{
  public async Task<IReadOnlyList<Station>> ReadAsync(GastronomyAppDbContext dbContext,
                                                      Guid festivalId,
                                                      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    return await dbContext.Stations
                          .AsNoTracking()
                          .Join(dbContext.FestivalStations.AsNoTracking(),
                                station => station.Id,
                                link => link.StationId,
                                (station, link) => new { Station = station, Link = link })
                          .Where(joined => joined.Link.FestivalId == festivalId
                                           && joined.Station.IsActive)
                          .OrderBy(joined => joined.Station.SortOrder)
                          .Select(joined => joined.Station)
                          .ToListAsync(cancellationToken);
  }

  public async Task<bool> IsAtTheFestivalAsync(GastronomyAppDbContext dbContext,
                                               Guid festivalId,
                                               Guid stationId,
                                               CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    return await dbContext.FestivalStations
                          .AsNoTracking()
                          .AnyAsync(link => link.FestivalId == festivalId && link.StationId == stationId,
                                    cancellationToken);
  }
}

public sealed class StationEstimateHandler
{
  private readonly ProductionEstimateCalculator _estimateCalculator;

  private readonly GastronomyAppDbContext _dbContext;
  private readonly RunningFestivalLookup _runningFestivalLookup;
  private readonly StationsAtTheFestivalReader _stationsReader;

  public StationEstimateHandler(GastronomyAppDbContext dbContext,
                                ProductionEstimateCalculator estimateCalculator,
                                RunningFestivalLookup runningFestivalLookup,
                                StationsAtTheFestivalReader stationsReader)
  {
    _dbContext = dbContext;
    _estimateCalculator = estimateCalculator;
    _runningFestivalLookup = runningFestivalLookup;
    _stationsReader = stationsReader;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    var festival = await _runningFestivalLookup.FindAsync(cancellationToken);

    if (festival is null)
    {
      return Results.Ok(new StationEstimateListView([]));
    }

    IReadOnlyList<Station> stations = await _stationsReader.ReadAsync(_dbContext, festival.Id, cancellationToken);

    List<QueuedItemRow> queued = await _dbContext.OrderItems
                                                 .AsNoTracking()
                                                 .Join(_dbContext.StationOrders.AsNoTracking(),
                                                       item => item.StationOrderId,
                                                       stationOrder => stationOrder.Id,
                                                       (item, stationOrder) => new { Item = item, StationOrder = stationOrder })
                                                 .Join(_dbContext.Orders.AsNoTracking(),
                                                       joined => joined.StationOrder.OrderId,
                                                       order => order.Id,
                                                       (joined, order) => new { joined.Item, joined.StationOrder, Order = order })
                                                 .Join(_dbContext.CatalogItems.AsNoTracking(),
                                                       joined => joined.Item.CatalogItemId,
                                                       catalogItem => catalogItem.Id,
                                                       (joined, catalogItem) => new
                                                                                {
                                                                                  joined.Item,
                                                                                  joined.StationOrder,
                                                                                  joined.Order,
                                                                                  CatalogItem = catalogItem
                                                                                })
                                                 .Where(joined => joined.Item.FulfilledAtUtc == null
                                                                  && joined.Order.FestivalId == festival.Id)
                                                 .Select(joined => new QueuedItemRow(joined.StationOrder.StationId,
                                                                                     joined.CatalogItem.ProductionMinutes,
                                                                                     joined.CatalogItem.IsQueueIndependent))
                                                 .ToListAsync(cancellationToken);

    return Results.Ok(new StationEstimateListView([
                                                    .. stations.Select(station => new StationEstimateView(station.Id,
                                                                                                           SumQueuedMinutes(queued, station.Id)))
                                                  ]));
  }

  private double SumQueuedMinutes(IReadOnlyCollection<QueuedItemRow> queued, Guid stationId)
  {
    return _estimateCalculator.SumQueuedMinutes(queued
                                               .Where(row => row.StationId == stationId)
                                               .Select(row => new QueuedWork(row.ProductionMinutes,
                                                                              row.IsQueueIndependent)));
  }
}

public sealed class StationQueueReader
{
  public async Task<IReadOnlyList<StationOrderQueueView>> LoadUnfinishedStationOrderQueueViewsAsync(GastronomyAppDbContext dbContext,
                                                                                                   Guid festivalId,
                                                                                                   Guid stationId,
                                                                                                   CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    List<Guid> stationOrderIdsWithOpenItems = await dbContext.OrderItems
                                                             .AsNoTracking()
                                                             .Where(item => item.FulfilledAtUtc == null)
                                                             .Select(item => item.StationOrderId)
                                                             .Distinct()
                                                             .ToListAsync(cancellationToken);

    List<StationOrder> stationOrders = await dbContext.StationOrders
                                                       .AsNoTracking()
                                                       .Join(dbContext.Orders.AsNoTracking(),
                                                             stationOrder => stationOrder.OrderId,
                                                             order => order.Id,
                                                             (stationOrder, order) => new { StationOrder = stationOrder, Order = order })
                                                       .Where(joined => joined.StationOrder.StationId == stationId
                                                                        && joined.Order.FestivalId == festivalId
                                                                        && stationOrderIdsWithOpenItems.Contains(joined.StationOrder.Id))
                                                       .Select(joined => joined.StationOrder)
                                                       .ToListAsync(cancellationToken);

    return await BuildStationOrderQueueViewsAsync(dbContext, stationOrders, cancellationToken);
  }

  public async Task<IReadOnlyList<StationOrderQueueView>> LoadFulfilledStationOrderQueueViewsAsync(GastronomyAppDbContext dbContext,
                                                                                                   Guid festivalId,
                                                                                                   Guid stationId,
                                                                                                   CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    List<Guid> stationOrderIdsWithFulfilledItems = await dbContext.OrderItems
                                                                  .AsNoTracking()
                                                                  .Where(item => item.FulfilledAtUtc != null)
                                                                  .Select(item => item.StationOrderId)
                                                                  .Distinct()
                                                                  .ToListAsync(cancellationToken);

    List<StationOrder> stationOrders = await dbContext.StationOrders
                                                       .AsNoTracking()
                                                       .Join(dbContext.Orders.AsNoTracking(),
                                                             stationOrder => stationOrder.OrderId,
                                                             order => order.Id,
                                                             (stationOrder, order) => new { StationOrder = stationOrder, Order = order })
                                                       .Where(joined => joined.StationOrder.StationId == stationId
                                                                        && joined.Order.FestivalId == festivalId
                                                                        && stationOrderIdsWithFulfilledItems.Contains(joined.StationOrder.Id))
                                                       .Select(joined => joined.StationOrder)
                                                       .ToListAsync(cancellationToken);

    return await BuildStationOrderQueueViewsAsync(dbContext, stationOrders, cancellationToken);
  }

  private async Task<IReadOnlyList<StationOrderQueueView>> BuildStationOrderQueueViewsAsync(GastronomyAppDbContext dbContext,
                                                                                            IReadOnlyCollection<StationOrder> stationOrders,
                                                                                            CancellationToken cancellationToken)
  {
    List<Guid> stationOrderIds = [.. stationOrders.Select(stationOrder => stationOrder.Id)];

    List<OrderItem> items = await dbContext.OrderItems
                                           .AsNoTracking()
                                           .Where(item => stationOrderIds.Contains(item.StationOrderId))
                                           .OrderBy(item => item.Id)
                                           .ToListAsync(cancellationToken);

    List<Guid> orderIds = [.. stationOrders.Select(stationOrder => stationOrder.OrderId).Distinct()];

    Dictionary<Guid, Order> orders = await dbContext.Orders
                                                    .AsNoTracking()
                                                    .Where(order => orderIds.Contains(order.Id))
                                                    .ToDictionaryAsync(order => order.Id, cancellationToken);

    return
    [
      .. stationOrders.Where(stationOrder => orders.ContainsKey(stationOrder.OrderId))
                      .OrderBy(stationOrder => stationOrder.StationOrderNumber)
                      .Select(stationOrder => BuildStationOrderQueueView(stationOrder, orders[stationOrder.OrderId], items))
    ];
  }

  private StationOrderQueueView BuildStationOrderQueueView(StationOrder stationOrder,
                                                           Order order,
                                                           IReadOnlyCollection<OrderItem> items)
  {
    List<OrderItem> stationOrderItems = [.. items.Where(item => item.StationOrderId == stationOrder.Id)];

    return new(stationOrder.Id,
               order.GlobalOrderNumber,
               stationOrder.StationOrderNumber,
               order.TableName,
               order.Note,
               stationOrder.DeliveryMode,
               order.CreatedAtUtc,
               stationOrder.IsHiddenFromAsItComesQueue,
               stationOrderItems.Count,
               stationOrderItems.Count(item => item.FulfilledAtUtc is not null),
               [
                 .. stationOrderItems.Select(item => new StationQueueItemView(item.Id,
                                                                              item.ItemName,
                                                                              item.Note,
                                                                              item.FulfilledAtUtc))
               ]);
  }
}

public sealed class StationQueueHandler
{
  private readonly IClock _clock;
  private readonly GastronomyAppDbContext _dbContext;
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly OrderItemFulfillmentService _fulfillmentService;
  private readonly OrderReader _orderReader;
  private readonly StationQueueReader _queueReader;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly RunningFestivalLookup _runningFestivalLookup;
  private readonly StationsAtTheFestivalReader _stationsReader;
  private readonly ITransactionRunner _transactionRunner;
  private readonly StationOrderVisibilityService _visibilityService;

  public StationQueueHandler(GastronomyAppDbContext dbContext,
                             StationQueueReader queueReader,
                             OrderItemFulfillmentService fulfillmentService,
                             StationOrderVisibilityService visibilityService,
                             OrderReader orderReader,
                             HubNotificationDispatcher dispatcher,
                             ResultEnvelope resultEnvelope,
                             RunningFestivalLookup runningFestivalLookup,
                             StationsAtTheFestivalReader stationsReader,
                             ITransactionRunner transactionRunner,
                             IClock clock)
  {
    _dbContext = dbContext;
    _transactionRunner = transactionRunner;
    _runningFestivalLookup = runningFestivalLookup;
    _stationsReader = stationsReader;
    _queueReader = queueReader;
    _fulfillmentService = fulfillmentService;
    _visibilityService = visibilityService;
    _orderReader = orderReader;
    _dispatcher = dispatcher;
    _resultEnvelope = resultEnvelope;
    _clock = clock;
  }

  public async Task<IResult> ListQueueAsync(StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationAtFestival, IResult> access = await FindStationAtFestivalAsync(caller, cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Failure;
    }

    return Results.Ok(await BuildStationQueueViewAsync(access.Value.Station, access.Value.FestivalId, cancellationToken));
  }

  private async Task<StationQueueView> BuildStationQueueViewAsync(Station station,
                                                                  Guid festivalId,
                                                                  CancellationToken cancellationToken)
  {
    IReadOnlyList<StationOrderQueueView> orders =
      await _queueReader.LoadUnfinishedStationOrderQueueViewsAsync(_dbContext, festivalId, station.Id, cancellationToken);

    IReadOnlyList<StationOrderQueueView> asItComes =
    [
      .. orders.Where(stationOrder => stationOrder.DeliveryMode == DeliveryMode.AsItComes
                                      && !stationOrder.IsHiddenFromAsItComesQueue)
    ];

    return new(new(station.Id, station.Name), orders, asItComes);
  }

  public async Task<IResult> ListFulfilledAsync(StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationAtFestival, IResult> access = await FindStationAtFestivalAsync(caller, cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Failure;
    }

    IReadOnlyList<StationOrderQueueView> stationOrders =
      await _queueReader.LoadFulfilledStationOrderQueueViewsAsync(_dbContext,
                                                                  access.Value.FestivalId,
                                                                  access.Value.Station.Id,
                                                                  cancellationToken);

    return Results.Ok(new StationFulfilledView(stationOrders));
  }

  public async Task<IResult> FulfillAsync(StationItemSelectionRequest request,
                                          StationDeviceCaller caller,
                                          CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationAtFestival, IResult> access = await FindStationAtFestivalAsync(caller, cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Failure;
    }

    List<Guid> selectedIds = [.. request.OrderItemIds ?? []];

    Result<FulfillmentResult, FulfillmentFailure> outcome =
      await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                        {
                                          var fulfillment = await ApplyFulfillmentAsync(selectedIds,
                                                                                        caller.StationId,
                                                                                        transactionCancellationToken);

                                          return new TransactionOutcome<Result<FulfillmentResult, FulfillmentFailure>>
                                                 {
                                                   Value = fulfillment,
                                                   ShouldCommit = fulfillment.IsSuccess
                                                 };
                                        },
                                        cancellationToken);

    if (!outcome.IsSuccess)
    {
      return Refuse(outcome.Failure);
    }

    List<Guid> affectedStationOrderIds = [.. outcome.Value.ChangedItems
                                                .Concat(outcome.Value.AlreadyFulfilled)
                                                .Select(item => item.StationOrderId)
                                                .Distinct()];

    await _dispatcher.PushStationOrdersChangedAsync(caller.StationId, cancellationToken);
    await PushOrderStatusOfAffectedOrdersAsync(affectedStationOrderIds, cancellationToken);

    return Results.Ok(await BuildStationQueueViewAsync(access.Value.Station, access.Value.FestivalId, cancellationToken));
  }

  public async Task<IResult> UnfulfillAsync(StationItemSelectionRequest request,
                                            StationDeviceCaller caller,
                                            CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationAtFestival, IResult> access = await FindStationAtFestivalAsync(caller, cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Failure;
    }

    List<Guid> selectedIds = [.. request.OrderItemIds ?? []];

    Result<FulfillmentResult, FulfillmentFailure> outcome =
      await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                        {
                                          var unfulfillment = await ApplyUnfulfillmentAsync(selectedIds,
                                                                                            caller.StationId,
                                                                                            transactionCancellationToken);

                                          return new TransactionOutcome<Result<FulfillmentResult, FulfillmentFailure>>
                                                 {
                                                   Value = unfulfillment,
                                                   ShouldCommit = unfulfillment.IsSuccess
                                                 };
                                        },
                                        cancellationToken);

    if (!outcome.IsSuccess)
    {
      return Refuse(outcome.Failure);
    }

    List<Guid> affectedStationOrderIds = [.. outcome.Value.ChangedItems
                                                .Select(item => item.StationOrderId)
                                                .Distinct()];

    await _dispatcher.PushStationOrdersChangedAsync(caller.StationId, cancellationToken);
    await PushOrderStatusOfAffectedOrdersAsync(affectedStationOrderIds, cancellationToken);

    return Results.Ok(await BuildStationQueueViewAsync(access.Value.Station, access.Value.FestivalId, cancellationToken));
  }

  public async Task<IResult> HideAsync(Guid stationOrderId,
                                       StationDeviceCaller caller,
                                       CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationAtFestival, IResult> access = await FindStationAtFestivalAsync(caller, cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Failure;
    }

    var stationOrder = await _dbContext.StationOrders
                                       .FirstOrDefaultAsync(candidate => candidate.Id == stationOrderId
                                                                         && candidate.StationId == caller.StationId
                                                                         && candidate.FestivalId == access.Value.FestivalId,
                                                            cancellationToken);

    if (stationOrder is null)
    {
      return _resultEnvelope.Problem(StatusCodes.Status422UnprocessableEntity,
                                     "UnprocessableEntity",
                                     "station.orderNotAtThisStation");
    }

    Result<StationOrder, StationOrderVisibilityFailure> hidden =
      _visibilityService.HideFromAsItComesQueue(stationOrder);

    if (!hidden.IsSuccess)
    {
      return _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                     "CannotHideTogetherOrder",
                                     "station.changeNotSaved");
    }

    await _dbContext.SaveChangesAsync(cancellationToken);

    await _dispatcher.PushStationOrdersChangedAsync(caller.StationId, cancellationToken);

    return Results.Ok(await BuildStationQueueViewAsync(access.Value.Station, access.Value.FestivalId, cancellationToken));
  }

  private async Task<Result<StationAtFestival, IResult>> FindStationAtFestivalAsync(StationDeviceCaller caller,
                                                                       CancellationToken cancellationToken)
  {
    var station = await _dbContext.Stations
                                  .AsNoTracking()
                                  .FirstOrDefaultAsync(candidate => candidate.Id == caller.StationId, cancellationToken);

    if (station is null)
    {
      return Result<StationAtFestival, IResult>.Failed(Results.Unauthorized());
    }

    FestivalStanding standing = await FindFestivalStandingAsync(caller.StationId, cancellationToken);

    if (standing.Refusal is not null)
    {
      return Result<StationAtFestival, IResult>.Failed(standing.Refusal);
    }

    return Result<StationAtFestival, IResult>.Success(new(station, standing.FestivalId));
  }

  private async Task<Result<FulfillmentResult, FulfillmentFailure>> ApplyFulfillmentAsync(IReadOnlyList<Guid> selectedIds,
                                                                                           Guid stationId,
                                                                                           CancellationToken cancellationToken)
  {
    List<OrderItem> itemsAtThisStation = await ItemsAtThisStationAsync(selectedIds, stationId, cancellationToken);

    Result<FulfillmentResult, FulfillmentFailure> fulfillment =
      _fulfillmentService.Fulfill(new() { OrderItemIds = selectedIds }, itemsAtThisStation, _clock.UtcNow);

    if (fulfillment.IsSuccess)
    {
      await _dbContext.SaveChangesAsync(cancellationToken);
    }

    return fulfillment;
  }

  private async Task<Result<FulfillmentResult, FulfillmentFailure>> ApplyUnfulfillmentAsync(IReadOnlyList<Guid> selectedIds,
                                                                                             Guid stationId,
                                                                                             CancellationToken cancellationToken)
  {
    List<OrderItem> itemsAtThisStation = await ItemsAtThisStationAsync(selectedIds, stationId, cancellationToken);

    Result<FulfillmentResult, FulfillmentFailure> unfulfillment =
      _fulfillmentService.Unfulfill(new() { OrderItemIds = selectedIds }, itemsAtThisStation);

    if (unfulfillment.IsSuccess)
    {
      await _dbContext.SaveChangesAsync(cancellationToken);
    }

    return unfulfillment;
  }

  private async Task<List<OrderItem>> ItemsAtThisStationAsync(IReadOnlyList<Guid> selectedIds,
                                                              Guid stationId,
                                                              CancellationToken cancellationToken)
  {
    List<Guid> stationOrderIdsAtThisStation = await _dbContext.StationOrders
                                                             .AsNoTracking()
                                                             .Where(stationOrder => stationOrder.StationId == stationId)
                                                             .Select(stationOrder => stationOrder.Id)
                                                             .ToListAsync(cancellationToken);

    return await _dbContext.OrderItems
                           .Where(item => selectedIds.Contains(item.Id)
                                          && stationOrderIdsAtThisStation.Contains(item.StationOrderId))
                           .ToListAsync(cancellationToken);
  }

  private async Task PushOrderStatusOfAffectedOrdersAsync(IReadOnlyCollection<Guid> stationOrderIds,
                                                          CancellationToken cancellationToken)
  {
    List<Guid> ids = [.. stationOrderIds];

    List<Guid> orderIds = await _dbContext.StationOrders
                                          .AsNoTracking()
                                          .Where(stationOrder => ids.Contains(stationOrder.Id))
                                          .Select(stationOrder => stationOrder.OrderId)
                                          .Distinct()
                                          .ToListAsync(cancellationToken);

    foreach (var orderId in orderIds)
    {
      var loaded = await _orderReader.LoadAsync(_dbContext, orderId, cancellationToken);

      if (loaded is not null)
      {
        await _dispatcher.OnOrderStatusChangedAsync(orderId, _orderReader.CalculateStatus(loaded), cancellationToken);
      }
    }
  }

  private async Task<FestivalStanding> FindFestivalStandingAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var festival = await _runningFestivalLookup.FindAsync(cancellationToken);

    if (festival is null)
    {
      return new(Guid.Empty,
                 _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                        "NoRunningFestival",
                                        "station.noFestivalIsRunning"));
    }

    var isAtTheFestival =
      await _stationsReader.IsAtTheFestivalAsync(_dbContext, festival.Id, stationId, cancellationToken);

    if (!isAtTheFestival)
    {
      return new(festival.Id,
                 _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                        "StationNotAtTheFestival",
                                        "station.notPartOfTheFestival"));
    }

    return new(festival.Id, null);
  }

  private IResult Refuse(FulfillmentFailure failure)
  {
    return failure.Reason switch
           {
             FulfillmentFailureReason.NoItemsSelected =>
               _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                      "ValidationFailed",
                                      "station.noItemsSelected"),
             FulfillmentFailureReason.UnknownOrderItemId =>
               _resultEnvelope.Problem(StatusCodes.Status422UnprocessableEntity,
                                      "UnprocessableEntity",
                                      "station.itemNotAtThisStation"),
             FulfillmentFailureReason.ItemNotFulfilled =>
               _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                      "ItemNotFulfilled",
                                      "station.changeNotSaved"),
             _ => new UnreachableCase().Throw<IResult>(failure.Reason)
           };
  }

  private sealed record StationAtFestival(Station Station, Guid FestivalId);
}

public sealed record FestivalStanding(Guid FestivalId, IResult? Refusal);
