using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public sealed class StationQueryHandler
{
  private readonly GastronomyAppDbContext dbContext;
  private readonly StationPrintabilityReader printabilityReader;
  private readonly PrinterStatusReader printerStatusReader;
  private readonly StationScreenDescriber screenDescriber;

  public StationQueryHandler(GastronomyAppDbContext dbContext,
                             StationPrintabilityReader printabilityReader,
                             StationScreenDescriber screenDescriber,
                             PrinterStatusReader printerStatusReader)
  {
    this.dbContext = dbContext;
    this.printabilityReader = printabilityReader;
    this.screenDescriber = screenDescriber;
    this.printerStatusReader = printerStatusReader;
  }

  public async Task<IResult> ListStationsAsync(CancellationToken cancellationToken)
  {
    IReadOnlyDictionary<Guid, StationPrintability> printability =
      await printabilityReader.ReadAsync(dbContext, cancellationToken);

    List<Station> stations = await dbContext.Stations
                                            .AsNoTracking()
                                            .Where(station => station.IsActive)
                                            .OrderBy(station => station.SortOrder)
                                            .ToListAsync(cancellationToken);

    List<StationView> views =
    [
      .. stations.Select(station => new StationView(station.Id,
                                                    station.Name,
                                                    station.SortOrder,
                                                    printability.TryGetValue(station.Id, out var stationPrintability)
                                                    && printabilityReader.CanPrintRightNow(stationPrintability)))
    ];

    return Results.Ok(new StationListView(views));
  }

  public async Task<IResult> ListStationOrdersAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return Results.Ok(new StationScreenListView(stationId,
                                                await screenDescriber.DescribeOpenStationOrdersAsync(dbContext, stationId, cancellationToken)));
  }

  public async Task<IResult> StatusAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var all = await printerStatusReader.ReadAsync(dbContext, cancellationToken);
    var selected = all.Stations.FirstOrDefault(view => view.StationId == stationId);

    return selected is null ? Results.NotFound() : Results.Ok(selected);
  }
}

public sealed record LatestPrintJob(Guid StationOrderId, PrintJobStatus Status, int CopyNumber);

public sealed class StationScreenDescriber
{
  private readonly HandledOnPaperPolicy handledOnPaperPolicy;
  private readonly OrderLineCollapser lineCollapser = new();
  private readonly StationPrintabilityReader printabilityReader;

  public StationScreenDescriber(HandledOnPaperPolicy handledOnPaperPolicy,
                                StationPrintabilityReader printabilityReader)
  {
    this.handledOnPaperPolicy = handledOnPaperPolicy;
    this.printabilityReader = printabilityReader;
  }

  public async Task<IReadOnlyList<StationScreenOrderView>> DescribeOpenStationOrdersAsync(GastronomyAppDbContext dbContext,
                                                                                          Guid stationId,
                                                                                          CancellationToken cancellationToken)
  {
    IReadOnlyDictionary<Guid, StationPrintability> printability =
      await printabilityReader.ReadAsync(dbContext, cancellationToken);

    var station = await dbContext.Stations
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(candidate => candidate.Id == stationId, cancellationToken);

    List<StationOrder> stationOrders = await dbContext.StationOrders
                                                      .AsNoTracking()
                                                      .Where(stationOrder => stationOrder.StationId == stationId)
                                                      .OrderBy(stationOrder => stationOrder.StationOrderNumber)
                                                      .ToListAsync(cancellationToken);

    List<Guid> stationOrderIds = [.. stationOrders.Select(stationOrder => stationOrder.Id)];

    Dictionary<Guid, LatestPrintJob> latestJobs =
      await LoadLatestPrintJobsAsync(dbContext, stationOrderIds, cancellationToken);

    HashSet<Guid> orderIds = [.. stationOrders.Select(stationOrder => stationOrder.OrderId)];

    Dictionary<Guid, Order> orders = await dbContext.Orders
                                                    .AsNoTracking()
                                                    .Where(order => orderIds.Contains(order.Id))
                                                    .ToDictionaryAsync(order => order.Id, cancellationToken);

    List<OrderItem> items = await dbContext.OrderItems
                                           .AsNoTracking()
                                           .Where(item => stationOrderIds.Contains(item.StationOrderId))
                                           .OrderBy(item => item.Id)
                                           .ToListAsync(cancellationToken);

    List<StationScreenOrderView> views = [];

    foreach (var stationOrder in stationOrders)
    {
      if (!orders.TryGetValue(stationOrder.OrderId, out var order))
      {
        continue;
      }

      var latest = latestJobs.TryGetValue(stationOrder.Id, out var job)
                     ? job
                     : new(stationOrder.Id, PrintJobStatus.Queued, 0);

      if (latest.Status is PrintJobStatus.Printed or PrintJobStatus.HandledOnPaper)
      {
        continue;
      }

      printability.TryGetValue(stationId, out var stationPrintability);
      var resolved = stationPrintability ?? UnknownStation();
      var canHandleOnPaper = handledOnPaperPolicy.CanHandleOnPaper(latest.Status, resolved);

      views.Add(new(stationOrder.Id,
                    order.Id,
                    stationId,
                    station?.Name ?? string.Empty,
                    stationOrder.StationOrderNumber,
                    order.GlobalOrderNumber,
                    order.TableName,
                    order.Note,
                    order.CreatedAtUtc,
                    latest.Status.ToString(),
                    latest.CopyNumber,
                    canHandleOnPaper,
                    canHandleOnPaper ? null : RefusalKeyFor(latest.Status),
                    [
                      .. lineCollapser
                        .Collapse([.. items.Where(item => item.StationOrderId == stationOrder.Id)],
                                  item => item.ItemName,
                                  item => item.Note)
                        .Select(collapsed => new StationOrderItemView(collapsed.Quantity,
                                                                      collapsed.Line.ItemName,
                                                                      collapsed.Line.Note))
                    ]));
    }

    return views;
  }

  public string RefusalKeyFor(PrintJobStatus status)
  {
    return status == PrintJobStatus.HandledOnPaper ? "station.alreadyTaken" : "station.takeRefused";
  }

  public async static Task<Dictionary<Guid, LatestPrintJob>> LoadLatestPrintJobsAsync(GastronomyAppDbContext dbContext,
                                                                                      IReadOnlyCollection<Guid> stationOrderIds,
                                                                                      CancellationToken cancellationToken)
  {
    List<Guid> ids = stationOrderIds.ToList();

    List<PrintJob> jobs = await dbContext.PrintJobs
                                         .AsNoTracking()
                                         .Where(job => ids.Contains(job.StationOrderId))
                                         .ToListAsync(cancellationToken);

    return jobs
          .GroupBy(job => job.StationOrderId)
          .ToDictionary(group => group.Key,
                        group =>
                        {
                          var latest = group.OrderByDescending(job => job.CopyNumber).First();
                          return new LatestPrintJob(group.Key, latest.Status, latest.CopyNumber);
                        });
  }

  private StationPrintability UnknownStation()
  {
    return new()
           {
             IsFaulty = true,
             IsOnline = false,
             IsPaperEnd = false,
             IsCoverOpen = false,
             IsInErrorState = false,
             IsEnabled = false
           };
  }
}

public sealed class StationHandOnPaperHandler
{
  private readonly GastronomyAppDbContext dbContext;
  private readonly HubNotificationDispatcher dispatcher;
  private readonly HandledOnPaperPolicy handledOnPaperPolicy;
  private readonly OrderReader orderReader;
  private readonly StationPrintabilityReader printabilityReader;
  private readonly ResultEnvelope resultEnvelope;
  private readonly StationScreenDescriber screenDescriber;
  private readonly PrintJobStateMachine stateMachine;
  private readonly ImmediateTransactionRunner transactionRunner = new();

  public StationHandOnPaperHandler(GastronomyAppDbContext dbContext,
                                   HandledOnPaperPolicy handledOnPaperPolicy,
                                   PrintJobStateMachine stateMachine,
                                   StationPrintabilityReader printabilityReader,
                                   StationScreenDescriber screenDescriber,
                                   OrderReader orderReader,
                                   HubNotificationDispatcher dispatcher,
                                   ResultEnvelope resultEnvelope)
  {
    this.dbContext = dbContext;
    this.handledOnPaperPolicy = handledOnPaperPolicy;
    this.stateMachine = stateMachine;
    this.printabilityReader = printabilityReader;
    this.screenDescriber = screenDescriber;
    this.orderReader = orderReader;
    this.dispatcher = dispatcher;
    this.resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> HandOnPaperAsync(Guid stationOrderId, CancellationToken cancellationToken)
  {
    var stationOrder = await dbContext.StationOrders
                                      .AsNoTracking()
                                      .FirstOrDefaultAsync(candidate => candidate.Id == stationOrderId, cancellationToken);

    if (stationOrder is null)
    {
      return Results.NotFound();
    }

    IReadOnlyDictionary<Guid, StationPrintability> printability =
      await printabilityReader.ReadAsync(dbContext, cancellationToken);

    if (!printability.TryGetValue(stationOrder.StationId, out var station))
    {
      return Results.NotFound();
    }

    Dictionary<Guid, LatestPrintJob> latestJobs = await StationScreenDescriber.LoadLatestPrintJobsAsync(dbContext,
                                                                                                        [stationOrderId],
                                                                                                        cancellationToken);

    if (!latestJobs.TryGetValue(stationOrderId, out var latest))
    {
      return Results.NotFound();
    }

    if (latest.Status is PrintJobStatus.HandledOnPaper)
    {
      return resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                    "HandOnPaperRefused",
                                    "station.alreadyTaken");
    }

    if (!handledOnPaperPolicy.CanHandleOnPaper(latest.Status, station))
    {
      return resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                    "HandOnPaperRefused",
                                    screenDescriber.RefusalKeyFor(latest.Status));
    }

    if (!stateMachine.CanTransition(latest.Status, PrintJobStatus.HandledOnPaper))
    {
      return resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                    "IllegalPrintJobTransition",
                                    "printJob.illegalTransition");
    }

    var orderId = stationOrder.OrderId;

    await transactionRunner.RunAsync(dbContext,
                                     async transactionCancellationToken =>
                                     {
                                       var tracked = await dbContext.PrintJobs
                                                                    .Where(job => job.StationOrderId == stationOrderId)
                                                                    .OrderByDescending(job => job.CopyNumber)
                                                                    .FirstAsync(transactionCancellationToken);

                                       tracked.Status = PrintJobStatus.HandledOnPaper;
                                       await dbContext.SaveChangesAsync(transactionCancellationToken);

                                       return new TransactionOutcome<bool> { Value = true, ShouldCommit = true };
                                     },
                                     cancellationToken);

    var loaded = await orderReader.LoadAsync(dbContext, orderId, cancellationToken);

    await dispatcher.OnPrintJobStatusChangedAsync(orderId,
                                                  stationOrderId,
                                                  PrintJobStatus.HandledOnPaper,
                                                  null,
                                                  cancellationToken);

    if (loaded is not null)
    {
      await dispatcher.OnOrderStatusChangedAsync(orderId, orderReader.StatusOf(loaded), cancellationToken);
    }

    return Results.Ok(new HandledOnPaperView(stationOrderId, PrintJobStatus.HandledOnPaper.ToString()));
  }
}

public sealed record HandledOnPaperView(Guid StationOrderId, string Status);
