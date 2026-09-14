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

public sealed class StationQueryHandler
{
  private readonly GastronomyAppDbContext _dbContext;
  private readonly RunningFestivalLookup _runningFestivalLookup;
  private readonly StationsAtTheFestivalReader _stationsReader;

  public StationQueryHandler(GastronomyAppDbContext dbContext,
                             RunningFestivalLookup runningFestivalLookup,
                             StationsAtTheFestivalReader stationsReader)
  {
    _dbContext = dbContext;
    _runningFestivalLookup = runningFestivalLookup;
    _stationsReader = stationsReader;
  }

  public async Task<IResult> ListStationsAsync(CancellationToken cancellationToken)
  {
    var festival = await _runningFestivalLookup.FindAsync(cancellationToken);

    IReadOnlyList<Station> stations = festival is null
                                        ? []
                                        : await _stationsReader.ReadAsync(_dbContext, festival.Id, cancellationToken);

    return Results.Ok(new StationListView([
                                            .. stations.Select(station => new StationView(station.Id,
                                                                                           station.Name,
                                                                                           station.SortOrder))
                                          ]));
  }
}

public sealed record QueuedItemRow(Guid StationId, int? ProductionMinutes);

public sealed class StationsAtTheFestivalReader
{
  public async Task<IReadOnlyList<Station>> ReadAsync(GastronomyAppDbContext dbContext,
                                                      Guid festivalId,
                                                      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    return await (from station in dbContext.Stations.AsNoTracking()
                  join link in dbContext.FestivalStations.AsNoTracking()
                    on station.Id equals link.StationId
                  where link.FestivalId == festivalId && station.IsActive
                  orderby station.SortOrder
                  select station)
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

    List<QueuedItemRow> queued = await (from item in _dbContext.OrderItems.AsNoTracking()
                                        join slice in _dbContext.StationOrders.AsNoTracking()
                                          on item.StationOrderId equals slice.Id
                                        join order in _dbContext.Orders.AsNoTracking()
                                          on slice.OrderId equals order.Id
                                        join catalogItem in _dbContext.CatalogItems.AsNoTracking()
                                          on item.CatalogItemId equals catalogItem.Id
                                        where item.FulfilledAtUtc == null
                                              && order.FestivalId == festival.Id
                                        select new QueuedItemRow(slice.StationId, catalogItem.ProductionMinutes))
                                       .ToListAsync(cancellationToken);

    return Results.Ok(new StationEstimateListView([
                                                    .. stations.Select(station => new StationEstimateView(station.Id,
                                                                                                           QueuedMinutesOf(queued, station.Id)))
                                                  ]));
  }

  private int QueuedMinutesOf(IReadOnlyCollection<QueuedItemRow> queued, Guid stationId)
  {
    return _estimateCalculator.QueuedMinutesOf(queued
                                              .Where(row => row.StationId == stationId)
                                              .Select(row => new QueuedWork(row.ProductionMinutes)));
  }
}

public sealed class StationQueueReader
{
  public async Task<IReadOnlyList<StationQueueSliceView>> DescribeUnfinishedAsync(GastronomyAppDbContext dbContext,
                                                                                   Guid festivalId,
                                                                                   Guid stationId,
                                                                                   CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    List<Guid> sliceIdsWithOpenItems = await dbContext.OrderItems
                                                      .AsNoTracking()
                                                      .Where(item => item.FulfilledAtUtc == null)
                                                      .Select(item => item.StationOrderId)
                                                      .Distinct()
                                                      .ToListAsync(cancellationToken);

    List<StationOrder> slices = await (from slice in dbContext.StationOrders.AsNoTracking()
                                       join order in dbContext.Orders.AsNoTracking()
                                         on slice.OrderId equals order.Id
                                       where slice.StationId == stationId
                                             && order.FestivalId == festivalId
                                             && sliceIdsWithOpenItems.Contains(slice.Id)
                                       select slice)
                                      .ToListAsync(cancellationToken);

    return await DescribeAsync(dbContext, slices, cancellationToken);
  }

  public async Task<IReadOnlyList<StationQueueSliceView>> DescribeFulfilledAsync(GastronomyAppDbContext dbContext,
                                                                                 Guid festivalId,
                                                                                 Guid stationId,
                                                                                 CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    List<Guid> sliceIdsWithFulfilledItems = await dbContext.OrderItems
                                                           .AsNoTracking()
                                                           .Where(item => item.FulfilledAtUtc != null)
                                                           .Select(item => item.StationOrderId)
                                                           .Distinct()
                                                           .ToListAsync(cancellationToken);

    List<StationOrder> slices = await (from slice in dbContext.StationOrders.AsNoTracking()
                                       join order in dbContext.Orders.AsNoTracking()
                                         on slice.OrderId equals order.Id
                                       where slice.StationId == stationId
                                             && order.FestivalId == festivalId
                                             && sliceIdsWithFulfilledItems.Contains(slice.Id)
                                       select slice)
                                      .ToListAsync(cancellationToken);

    return await DescribeAsync(dbContext, slices, cancellationToken);
  }

  private async Task<IReadOnlyList<StationQueueSliceView>> DescribeAsync(GastronomyAppDbContext dbContext,
                                                                         IReadOnlyCollection<StationOrder> slices,
                                                                         CancellationToken cancellationToken)
  {
    List<Guid> sliceIds = [.. slices.Select(slice => slice.Id)];

    List<OrderItem> items = await dbContext.OrderItems
                                           .AsNoTracking()
                                           .Where(item => sliceIds.Contains(item.StationOrderId))
                                           .OrderBy(item => item.Id)
                                           .ToListAsync(cancellationToken);

    List<Guid> orderIds = [.. slices.Select(slice => slice.OrderId).Distinct()];

    Dictionary<Guid, Order> orders = await dbContext.Orders
                                                    .AsNoTracking()
                                                    .Where(order => orderIds.Contains(order.Id))
                                                    .ToDictionaryAsync(order => order.Id, cancellationToken);

    return
    [
      .. slices.Where(slice => orders.ContainsKey(slice.OrderId))
               .OrderBy(slice => slice.StationOrderNumber)
               .Select(slice => DescribeSlice(slice, orders[slice.OrderId], items))
    ];
  }

  private StationQueueSliceView DescribeSlice(StationOrder slice,
                                              Order order,
                                              IReadOnlyCollection<OrderItem> items)
  {
    List<OrderItem> itemsOfTheSlice = [.. items.Where(item => item.StationOrderId == slice.Id)];

    return new(slice.Id,
               order.GlobalOrderNumber,
               slice.StationOrderNumber,
               order.TableName,
               order.Note,
               slice.DeliveryMode,
               order.CreatedAtUtc,
               slice.IsHiddenFromAsItComesQueue,
               itemsOfTheSlice.Count,
               itemsOfTheSlice.Count(item => item.FulfilledAtUtc is not null),
               [
                 .. itemsOfTheSlice.Select(item => new StationQueueItemView(item.Id,
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
  private readonly ImmediateTransactionRunner _transactionRunner = new();
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
                             IClock clock)
  {
    _dbContext = dbContext;
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

    Result<StationAtFestival, IResult> access = await AccessOfAsync(caller, cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Failure;
    }

    return Results.Ok(await DescribeQueueAsync(access.Value.Station, access.Value.FestivalId, cancellationToken));
  }

  private async Task<StationQueueView> DescribeQueueAsync(Station station,
                                                          Guid festivalId,
                                                          CancellationToken cancellationToken)
  {
    IReadOnlyList<StationQueueSliceView> orders =
      await _queueReader.DescribeUnfinishedAsync(_dbContext, festivalId, station.Id, cancellationToken);

    IReadOnlyList<StationQueueSliceView> asItComes =
    [
      .. orders.Where(slice => slice.DeliveryMode == DeliveryMode.AsItComes && !slice.IsHiddenFromAsItComesQueue)
    ];

    return new(new(station.Id, station.Name), orders, asItComes);
  }

  public async Task<IResult> ListFulfilledAsync(StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationAtFestival, IResult> access = await AccessOfAsync(caller, cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Failure;
    }

    IReadOnlyList<StationQueueSliceView> slices =
      await _queueReader.DescribeFulfilledAsync(_dbContext,
                                                access.Value.FestivalId,
                                                access.Value.Station.Id,
                                                cancellationToken);

    return Results.Ok(new StationFulfilledView(slices));
  }

  public async Task<IResult> FulfillAsync(StationItemSelectionRequest request,
                                          StationDeviceCaller caller,
                                          CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationAtFestival, IResult> access = await AccessOfAsync(caller, cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Failure;
    }

    List<Guid> selectedIds = [.. request.OrderItemIds ?? []];

    Result<FulfillmentResult, FulfillmentFailure> outcome =
      await _transactionRunner.RunAsync(_dbContext,
                                        async transactionCancellationToken =>
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

    List<Guid> affectedSliceIds = [.. outcome.Value.ChangedItems
                                          .Concat(outcome.Value.AlreadyFulfilled)
                                          .Select(item => item.StationOrderId)
                                          .Distinct()];

    await _dispatcher.PushStationOrdersChangedAsync(caller.StationId, cancellationToken);
    await PushOrderStatusOfAffectedOrdersAsync(affectedSliceIds, cancellationToken);

    return Results.Ok(await DescribeQueueAsync(access.Value.Station, access.Value.FestivalId, cancellationToken));
  }

  public async Task<IResult> UnfulfillAsync(StationItemSelectionRequest request,
                                            StationDeviceCaller caller,
                                            CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationAtFestival, IResult> access = await AccessOfAsync(caller, cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Failure;
    }

    List<Guid> selectedIds = [.. request.OrderItemIds ?? []];

    Result<FulfillmentResult, FulfillmentFailure> outcome =
      await _transactionRunner.RunAsync(_dbContext,
                                        async transactionCancellationToken =>
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

    List<Guid> affectedSliceIds = [.. outcome.Value.ChangedItems
                                          .Select(item => item.StationOrderId)
                                          .Distinct()];

    await _dispatcher.PushStationOrdersChangedAsync(caller.StationId, cancellationToken);
    await PushOrderStatusOfAffectedOrdersAsync(affectedSliceIds, cancellationToken);

    return Results.Ok(await DescribeQueueAsync(access.Value.Station, access.Value.FestivalId, cancellationToken));
  }

  public async Task<IResult> HideAsync(Guid stationOrderId,
                                       StationDeviceCaller caller,
                                       CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationAtFestival, IResult> access = await AccessOfAsync(caller, cancellationToken);

    if (!access.IsSuccess)
    {
      return access.Failure;
    }

    var slice = await _dbContext.StationOrders
                                .FirstOrDefaultAsync(candidate => candidate.Id == stationOrderId
                                                                  && candidate.StationId == caller.StationId
                                                                  && candidate.FestivalId == access.Value.FestivalId,
                                                     cancellationToken);

    if (slice is null)
    {
      return _resultEnvelope.Problem(StatusCodes.Status422UnprocessableEntity,
                                     "UnprocessableEntity",
                                     "station.orderNotAtThisStation");
    }

    Result<StationOrder, StationOrderVisibilityFailure> hidden = _visibilityService.HideFromAsItComesQueue(slice);

    if (!hidden.IsSuccess)
    {
      return _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                     "CannotHideTogetherOrder",
                                     "station.changeNotSaved");
    }

    await _dbContext.SaveChangesAsync(cancellationToken);

    await _dispatcher.PushStationOrdersChangedAsync(caller.StationId, cancellationToken);

    return Results.Ok(await DescribeQueueAsync(access.Value.Station, access.Value.FestivalId, cancellationToken));
  }

  private async Task<Result<StationAtFestival, IResult>> AccessOfAsync(StationDeviceCaller caller,
                                                                       CancellationToken cancellationToken)
  {
    var station = await _dbContext.Stations
                                  .AsNoTracking()
                                  .FirstOrDefaultAsync(candidate => candidate.Id == caller.StationId, cancellationToken);

    if (station is null)
    {
      return Result<StationAtFestival, IResult>.Failed(Results.Unauthorized());
    }

    FestivalStanding standing = await StandingOfAsync(caller.StationId, cancellationToken);

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
    List<Guid> sliceIdsOfThisStation = await _dbContext.StationOrders
                                                       .AsNoTracking()
                                                       .Where(slice => slice.StationId == stationId)
                                                       .Select(slice => slice.Id)
                                                       .ToListAsync(cancellationToken);

    return await _dbContext.OrderItems
                           .Where(item => selectedIds.Contains(item.Id)
                                          && sliceIdsOfThisStation.Contains(item.StationOrderId))
                           .ToListAsync(cancellationToken);
  }

  private async Task PushOrderStatusOfAffectedOrdersAsync(IReadOnlyCollection<Guid> stationOrderIds,
                                                          CancellationToken cancellationToken)
  {
    List<Guid> ids = [.. stationOrderIds];

    List<Guid> orderIds = await _dbContext.StationOrders
                                          .AsNoTracking()
                                          .Where(slice => ids.Contains(slice.Id))
                                          .Select(slice => slice.OrderId)
                                          .Distinct()
                                          .ToListAsync(cancellationToken);

    foreach (var orderId in orderIds)
    {
      var loaded = await _orderReader.LoadAsync(_dbContext, orderId, cancellationToken);

      if (loaded is not null)
      {
        await _dispatcher.OnOrderStatusChangedAsync(orderId, _orderReader.StatusOf(loaded), cancellationToken);
      }
    }
  }

  private async Task<FestivalStanding> StandingOfAsync(Guid stationId, CancellationToken cancellationToken)
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
             _ => new Never().OfType<IResult>(failure.Reason)
           };
  }

  private sealed record StationAtFestival(Station Station, Guid FestivalId);
}

public sealed record FestivalStanding(Guid FestivalId, IResult? Refusal);
