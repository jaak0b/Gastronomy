using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Printing;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public sealed class StationOrderActionHandler
{
  private readonly GastronomyAppDbContext _dbContext;
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly OrderReader _orderReader;
  private readonly IPrinterFleet _printerFleet;
  private readonly PrintJobEnqueuer _printJobEnqueuer;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly PrintJobStateMachine _stateMachine;
  private readonly ImmediateTransactionRunner _transactionRunner = new();

  public StationOrderActionHandler(GastronomyAppDbContext dbContext,
                                   OrderReader orderReader,
                                   PrintJobStateMachine stateMachine,
                                   PrintJobEnqueuer printJobEnqueuer,
                                   IPrinterFleet printerFleet,
                                   HubNotificationDispatcher dispatcher,
                                   ResultEnvelope resultEnvelope)
  {
    _dbContext = dbContext;
    _orderReader = orderReader;
    _stateMachine = stateMachine;
    _printJobEnqueuer = printJobEnqueuer;
    _printerFleet = printerFleet;
    _dispatcher = dispatcher;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> ResolveUnknownAsync(Guid orderId,
                                                 Guid stationOrderId,
                                                 ResolveUnknownPrintRequest request,
                                                 Guid? callerStaffMemberId,
                                                 CancellationToken cancellationToken)
  {
    var order = await _dbContext.Orders
                               .AsNoTracking()
                               .FirstOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

    if (order is null)
    {
      return Results.NotFound();
    }

    if (callerStaffMemberId is not null && order.StaffMemberId != callerStaffMemberId)
    {
      return _resultEnvelope.Problem(StatusCodes.Status403Forbidden,
                                    "NotYourOrder",
                                    "order.notYours");
    }

    var latest = await LatestPrintJobAsync(stationOrderId, orderId, cancellationToken);
    if (latest is null)
    {
      return Results.NotFound();
    }

    if (latest.Status != PrintJobStatus.Unknown)
    {
      return _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                    "QuestionAlreadyAnswered",
                                    "printJob.questionAlreadyAnswered");
    }

    var target = request.SlipIsOnThePile
                   ? PrintJobStatus.Printed
                   : PrintJobStatus.Queued;

    if (!_stateMachine.CanTransition(latest.Status, target))
    {
      return _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                    "IllegalPrintJobTransition",
                                    "printJob.illegalTransition");
    }

    var applied = await _transactionRunner.RunAsync(_dbContext,
                                                   async transactionCancellationToken =>
                                                   {
                                                     var tracked = await _dbContext.PrintJobs
                                                                                  .FirstAsync(candidate => candidate.Id == latest.Id, transactionCancellationToken);

                                                     if (tracked.Status != PrintJobStatus.Unknown)
                                                     {
                                                       return new() { Value = false, ShouldCommit = false };
                                                     }

                                                     tracked.Status = target;
                                                     await _dbContext.SaveChangesAsync(transactionCancellationToken);

                                                     return new TransactionOutcome<bool> { Value = true, ShouldCommit = true };
                                                   },
                                                   cancellationToken);

    if (!applied)
    {
      return _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                    "QuestionAlreadyAnswered",
                                    "printJob.questionAlreadyAnswered");
    }

    if (target == PrintJobStatus.Queued)
    {
      await _printJobEnqueuer.EnqueueWithoutFailingTheCallerAsync(stationOrderId, cancellationToken);
    }

    var loaded = (await _orderReader.LoadAsync(_dbContext, orderId, cancellationToken))!;

    await _dispatcher.OnPrintJobStatusChangedAsync(orderId, stationOrderId, target, null, cancellationToken);
    await _dispatcher.OnOrderStatusChangedAsync(orderId, _orderReader.StatusOf(loaded), cancellationToken);

    return Results.Ok(_orderReader.DescribeStationOrders(loaded).First(view => view.StationOrderId == stationOrderId));
  }

  public async Task<IResult> PrintAnotherCopyAsync(Guid orderId,
                                                   Guid stationOrderId,
                                                   Guid? callerStaffMemberId,
                                                   CancellationToken cancellationToken)
  {
    var order = await _dbContext.Orders
                               .AsNoTracking()
                               .FirstOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

    if (order is null)
    {
      return Results.NotFound();
    }

    if (callerStaffMemberId is not null && order.StaffMemberId != callerStaffMemberId)
    {
      return _resultEnvelope.Problem(StatusCodes.Status403Forbidden,
                                    "NotYourOrder",
                                    "order.notYours");
    }

    var latest = await LatestPrintJobAsync(stationOrderId, orderId, cancellationToken);
    if (latest is null)
    {
      return Results.NotFound();
    }

    if (latest.Status is not (PrintJobStatus.Failed or PrintJobStatus.Printed))
    {
      return _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                    "AnotherCopyNotAllowed",
                                    "printJob.reprintNotAllowed");
    }

    PrintJobEnsured ensured;

    try
    {
      ensured = await _printerFleet.EnqueueAsync(stationOrderId, cancellationToken);
    }
    catch (UnknownStationOrderException)
    {
      return Results.NotFound();
    }

    if (!ensured.WasCreated)
    {
      return _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                    "PrintJobAlreadyRunning",
                                    "printJob.printJobAlreadyRunning");
    }

    var loaded = (await _orderReader.LoadAsync(_dbContext, orderId, cancellationToken))!;

    return Results.Json(_orderReader.DescribeStationOrders(loaded).First(view => view.StationOrderId == stationOrderId),
                        statusCode: StatusCodes.Status202Accepted);
  }

  private async Task<PrintJob?> LatestPrintJobAsync(Guid stationOrderId,
                                                    Guid orderId,
                                                    CancellationToken cancellationToken)
  {
    var belongsToOrder = await _dbContext.StationOrders
                                        .AsNoTracking()
                                        .AnyAsync(stationOrder => stationOrder.Id == stationOrderId && stationOrder.OrderId == orderId,
                                                  cancellationToken);

    if (!belongsToOrder)
    {
      return null;
    }

    return await _dbContext.PrintJobs
                          .AsNoTracking()
                          .Where(job => job.StationOrderId == stationOrderId)
                          .OrderByDescending(job => job.CopyNumber)
                          .FirstOrDefaultAsync(cancellationToken);
  }
}
