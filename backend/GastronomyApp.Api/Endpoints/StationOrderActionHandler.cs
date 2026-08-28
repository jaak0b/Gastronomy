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
  private readonly GastronomyAppDbContext dbContext;
  private readonly OrderReader orderReader;
  private readonly PrintJobStateMachine stateMachine;
  private readonly PrintJobEnqueuer printJobEnqueuer;
  private readonly IPrinterFleet printerFleet;
  private readonly HubNotificationDispatcher dispatcher;
  private readonly ResultEnvelope resultEnvelope;
  private readonly ImmediateTransactionRunner transactionRunner = new();

  public StationOrderActionHandler(
      GastronomyAppDbContext dbContext,
      OrderReader orderReader,
      PrintJobStateMachine stateMachine,
      PrintJobEnqueuer printJobEnqueuer,
      IPrinterFleet printerFleet,
      HubNotificationDispatcher dispatcher,
      ResultEnvelope resultEnvelope)
  {
    this.dbContext = dbContext;
    this.orderReader = orderReader;
    this.stateMachine = stateMachine;
    this.printJobEnqueuer = printJobEnqueuer;
    this.printerFleet = printerFleet;
    this.dispatcher = dispatcher;
    this.resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> ResolveUnknownAsync(
      Guid orderId,
      Guid stationOrderId,
      ResolveUnknownPrintRequest request,
      Guid? callerStaffMemberId,
      CancellationToken cancellationToken)
  {
    Order? order = await dbContext.Orders
        .AsNoTracking()
        .FirstOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

    if (order is null)
    {
      return Results.NotFound();
    }

    if (callerStaffMemberId is not null && order.StaffMemberId != callerStaffMemberId)
    {
      return resultEnvelope.Problem(
          StatusCodes.Status403Forbidden,
          "NotYourOrder",
          "order.notYours");
    }

    PrintJob? latest = await LatestPrintJobAsync(stationOrderId, orderId, cancellationToken);
    if (latest is null)
    {
      return Results.NotFound();
    }

    if (latest.Status != PrintJobStatus.Unknown)
    {
      return resultEnvelope.Problem(
          StatusCodes.Status409Conflict,
          "QuestionAlreadyAnswered",
          "printJob.questionAlreadyAnswered");
    }

    PrintJobStatus target = request.SlipIsOnThePile
        ? PrintJobStatus.Printed
        : PrintJobStatus.Queued;

    if (!stateMachine.CanTransition(latest.Status, target))
    {
      return resultEnvelope.Problem(
          StatusCodes.Status409Conflict,
          "IllegalPrintJobTransition",
          "printJob.illegalTransition");
    }

    bool applied = await transactionRunner.RunAsync(
        dbContext,
        async transactionCancellationToken =>
        {
          PrintJob tracked = await dbContext.PrintJobs
                  .FirstAsync(candidate => candidate.Id == latest.Id, transactionCancellationToken);

          if (tracked.Status != PrintJobStatus.Unknown)
          {
            return new TransactionOutcome<bool> { Value = false, ShouldCommit = false };
          }

          tracked.Status = target;
          await dbContext.SaveChangesAsync(transactionCancellationToken);

          return new TransactionOutcome<bool> { Value = true, ShouldCommit = true };
        },
        cancellationToken);

    if (!applied)
    {
      return resultEnvelope.Problem(
          StatusCodes.Status409Conflict,
          "QuestionAlreadyAnswered",
          "printJob.questionAlreadyAnswered");
    }

    if (target == PrintJobStatus.Queued)
    {
      await printJobEnqueuer.EnqueueWithoutFailingTheCallerAsync(stationOrderId, cancellationToken);
    }

    LoadedOrder loaded = (await orderReader.LoadAsync(dbContext, orderId, cancellationToken))!;

    await dispatcher.OnPrintJobStatusChangedAsync(orderId, stationOrderId, target, null, cancellationToken);
    await dispatcher.OnOrderStatusChangedAsync(orderId, orderReader.StatusOf(loaded), cancellationToken);

    return Results.Ok(
        orderReader.DescribeStationOrders(loaded).First(view => view.StationOrderId == stationOrderId));
  }

  public async Task<IResult> PrintAnotherCopyAsync(
      Guid orderId,
      Guid stationOrderId,
      Guid? callerStaffMemberId,
      CancellationToken cancellationToken)
  {
    Order? order = await dbContext.Orders
        .AsNoTracking()
        .FirstOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

    if (order is null)
    {
      return Results.NotFound();
    }

    if (callerStaffMemberId is not null && order.StaffMemberId != callerStaffMemberId)
    {
      return resultEnvelope.Problem(
          StatusCodes.Status403Forbidden,
          "NotYourOrder",
          "order.notYours");
    }

    PrintJob? latest = await LatestPrintJobAsync(stationOrderId, orderId, cancellationToken);
    if (latest is null)
    {
      return Results.NotFound();
    }

    if (latest.Status is not (PrintJobStatus.Failed or PrintJobStatus.Printed))
    {
      return resultEnvelope.Problem(
          StatusCodes.Status409Conflict,
          "AnotherCopyNotAllowed",
          "printJob.anotherCopyNotAllowed");
    }

    PrintJobEnsured ensured;

    try
    {
      ensured = await printerFleet.EnqueueAsync(stationOrderId, cancellationToken);
    }
    catch (UnknownStationOrderException)
    {
      return Results.NotFound();
    }

    if (!ensured.WasCreated)
    {
      return resultEnvelope.Problem(
          StatusCodes.Status409Conflict,
          "PrintJobAlreadyRunning",
          "printJob.alreadyRunning");
    }

    LoadedOrder loaded = (await orderReader.LoadAsync(dbContext, orderId, cancellationToken))!;

    return Results.Json(
        orderReader.DescribeStationOrders(loaded).First(view => view.StationOrderId == stationOrderId),
        statusCode: StatusCodes.Status202Accepted);
  }

  private async Task<PrintJob?> LatestPrintJobAsync(
      Guid stationOrderId,
      Guid orderId,
      CancellationToken cancellationToken)
  {
    bool belongsToOrder = await dbContext.StationOrders
        .AsNoTracking()
        .AnyAsync(
            stationOrder => stationOrder.Id == stationOrderId && stationOrder.OrderId == orderId,
            cancellationToken);

    if (!belongsToOrder)
    {
      return null;
    }

    return await dbContext.PrintJobs
        .AsNoTracking()
        .Where(job => job.StationOrderId == stationOrderId)
        .OrderByDescending(job => job.CopyNumber)
        .FirstOrDefaultAsync(cancellationToken);
  }
}
