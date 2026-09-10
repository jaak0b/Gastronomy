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

public sealed record QueuedItemRow(Guid StationId, ProductionStatus Status, int? ProductionMinutes);

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
                                        where item.ProductionStatus != ProductionStatus.Finished
                                              && order.FestivalId == festival.Id
                                        select new QueuedItemRow(slice.StationId,
                                                                 item.ProductionStatus,
                                                                 catalogItem.ProductionMinutes))
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
                                              .Select(row => new QueuedWork(row.Status, row.ProductionMinutes)));
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

    List<Guid> sliceIdsWithWorkLeft = await dbContext.OrderItems
                                                     .AsNoTracking()
                                                     .Where(item => item.ProductionStatus != ProductionStatus.Finished)
                                                     .Select(item => item.StationOrderId)
                                                     .Distinct()
                                                     .ToListAsync(cancellationToken);

    List<StationOrder> slices = await (from slice in dbContext.StationOrders.AsNoTracking()
                                       join order in dbContext.Orders.AsNoTracking()
                                         on slice.OrderId equals order.Id
                                       where slice.StationId == stationId
                                             && order.FestivalId == festivalId
                                             && sliceIdsWithWorkLeft.Contains(slice.Id)
                                       select slice)
                                      .ToListAsync(cancellationToken);

    return await DescribeAsync(dbContext, slices, cancellationToken);
  }

  public async Task<IReadOnlyList<StationQueueSliceView>> DescribeSlicesAsync(GastronomyAppDbContext dbContext,
                                                                              IReadOnlyCollection<Guid> stationOrderIds,
                                                                              CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);
    ArgumentNullException.ThrowIfNull(stationOrderIds);

    List<Guid> ids = [.. stationOrderIds];

    List<StationOrder> slices = await dbContext.StationOrders
                                               .AsNoTracking()
                                               .Where(slice => ids.Contains(slice.Id))
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
               .Select(slice => new StationQueueSliceView(slice.Id,
                                                          orders[slice.OrderId].GlobalOrderNumber,
                                                          slice.StationOrderNumber,
                                                          orders[slice.OrderId].TableName,
                                                          orders[slice.OrderId].Note,
                                                          slice.DeliveryMode,
                                                          orders[slice.OrderId].CreatedAtUtc,
                                                          [
                                                            .. items.Where(item => item.StationOrderId == slice.Id)
                                                                    .Select(item => new StationQueueItemView(item.Id,
                                                                                                              item.ItemName,
                                                                                                              item.Note,
                                                                                                              item.ProductionStatus))
                                                          ]))
    ];
  }
}

public sealed class StationQueueHandler
{
  private readonly IClock _clock;
  private readonly GastronomyAppDbContext _dbContext;
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly OrderReader _orderReader;
  private readonly OrderItemProductionService _productionService;
  private readonly StationQueueReader _queueReader;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly RunningFestivalLookup _runningFestivalLookup;
  private readonly StationsAtTheFestivalReader _stationsReader;
  private readonly ImmediateTransactionRunner _transactionRunner = new();

  public StationQueueHandler(GastronomyAppDbContext dbContext,
                             StationQueueReader queueReader,
                             OrderItemProductionService productionService,
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
    _productionService = productionService;
    _orderReader = orderReader;
    _dispatcher = dispatcher;
    _resultEnvelope = resultEnvelope;
    _clock = clock;
  }

  public async Task<IResult> ListQueueAsync(StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    var station = await _dbContext.Stations
                                  .AsNoTracking()
                                  .FirstOrDefaultAsync(candidate => candidate.Id == caller.StationId, cancellationToken);

    if (station is null)
    {
      return Results.Unauthorized();
    }

    FestivalStanding standing = await StandingOfAsync(caller.StationId, cancellationToken);

    if (standing.Refusal is not null)
    {
      return standing.Refusal;
    }

    IReadOnlyList<StationQueueSliceView> slices =
      await _queueReader.DescribeUnfinishedAsync(_dbContext,
                                                 standing.FestivalId,
                                                 station.Id,
                                                 cancellationToken);

    return Results.Ok(new StationQueueView(new(station.Id, station.Name), slices));
  }

  public async Task<IResult> AdvanceAsync(StationItemStatusRequest request,
                                          StationDeviceCaller caller,
                                          CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    FestivalStanding standing = await StandingOfAsync(caller.StationId, cancellationToken);

    if (standing.Refusal is not null)
    {
      return standing.Refusal;
    }

    List<Guid> selectedIds = [.. request.OrderItemIds ?? []];

    Result<ProductionStatusChangeResult, ProductionStatusFailure> outcome =
      await _transactionRunner.RunAsync(_dbContext,
                                        async transactionCancellationToken =>
                                        {
                                          var change = await ApplyAsync(request.Status,
                                                                        selectedIds,
                                                                        caller.StationId,
                                                                        transactionCancellationToken);

                                          return new TransactionOutcome<Result<ProductionStatusChangeResult, ProductionStatusFailure>>
                                                 {
                                                   Value = change,
                                                   ShouldCommit = change.IsSuccess
                                                 };
                                        },
                                        cancellationToken);

    if (!outcome.IsSuccess)
    {
      return Refuse(outcome.Failure);
    }

    List<Guid> affectedSliceIds = [.. outcome.Value.ChangedItems
                                          .Concat(outcome.Value.AlreadyAtTheTargetStatus)
                                          .Select(item => item.StationOrderId)
                                          .Distinct()];

    IReadOnlyList<StationQueueSliceView> slices =
      await _queueReader.DescribeSlicesAsync(_dbContext, affectedSliceIds, cancellationToken);

    await _dispatcher.PushStationOrdersChangedAsync(caller.StationId, cancellationToken);
    await PushOrderStatusOfAffectedOrdersAsync(affectedSliceIds, cancellationToken);

    return Results.Ok(new StationItemStatusView(SharedTableNameOf(slices), slices));
  }

  private async Task<Result<ProductionStatusChangeResult, ProductionStatusFailure>> ApplyAsync(ProductionStatus targetStatus,
                                                                                               IReadOnlyList<Guid> selectedIds,
                                                                                               Guid stationId,
                                                                                               CancellationToken cancellationToken)
  {
    List<Guid> sliceIdsOfThisStation = await _dbContext.StationOrders
                                                       .AsNoTracking()
                                                       .Where(slice => slice.StationId == stationId)
                                                       .Select(slice => slice.Id)
                                                       .ToListAsync(cancellationToken);

    List<OrderItem> itemsAtThisStation = await _dbContext.OrderItems
                                                         .Where(item => selectedIds.Contains(item.Id)
                                                                        && sliceIdsOfThisStation.Contains(item.StationOrderId))
                                                         .ToListAsync(cancellationToken);

    Result<ProductionStatusChangeResult, ProductionStatusFailure> change =
      _productionService.Advance(new() { OrderItemIds = selectedIds, TargetStatus = targetStatus },
                                 itemsAtThisStation,
                                 _clock.UtcNow);

    if (change.IsSuccess)
    {
      await _dbContext.SaveChangesAsync(cancellationToken);
    }

    return change;
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

  private string? SharedTableNameOf(IReadOnlyCollection<StationQueueSliceView> slices)
  {
    List<string> tableNames = [.. slices.Select(slice => slice.TableName).Distinct(StringComparer.Ordinal)];

    return tableNames.Count == 1 ? tableNames[0] : null;
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

  private IResult Refuse(ProductionStatusFailure failure)
  {
    return failure.Reason switch
           {
             ProductionStatusFailureReason.NoItemsSelected =>
               _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                      "ValidationFailed",
                                      "station.noItemsSelected"),
             ProductionStatusFailureReason.UnknownOrderItemId =>
               _resultEnvelope.Problem(StatusCodes.Status422UnprocessableEntity,
                                      "UnprocessableEntity",
                                      "station.itemNotAtThisStation"),
             ProductionStatusFailureReason.TransitionNotAllowed =>
               _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                      "ProductionStatusAlreadyPassed",
                                      "station.statusAlreadyPassed"),
             _ => new Never().OfType<IResult>(failure.Reason)
           };
  }
}

public sealed record FestivalStanding(Guid FestivalId, IResult? Refusal);
