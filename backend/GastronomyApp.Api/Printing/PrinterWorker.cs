using GastronomyApp.Api.Options;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Printing;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Printing;

public sealed record PreflightResult(bool IsBlocking, PrinterStatusSnapshot Snapshot, PrintFailureReason? BlockingReason);

public sealed record PendingTestPrint(Guid PrintJobId);

public sealed class PrinterWorker
{
  private readonly IPrintCallbacks _callbacks;
  private readonly StationCircuitBreaker _circuitBreaker = new();
  private readonly IPrinterWorkerDataAccess _dataAccess;
  private readonly PrinterWorkerDomainServices _domainServices;
  private readonly IPrinterDriver _driver;
  private readonly Lock _guard = new();

  private readonly AppLanguage _language;
  private readonly OrderLineCollapser _lineCollapser = new();
  private readonly ILogger<PrinterWorker> _logger;
  private readonly List<Guid> _pending = [];
  private readonly List<PendingTestPrint> _pendingTestPrints = [];
  private readonly ReconnectBackoff _reconnectBackoff = new();
  private readonly EscPosSlipRenderer _renderer;
  private readonly TimeProvider _timeProvider;
  private PrinterStatusSnapshot? _cachedStatus;
  private DateTimeOffset _lastHeartbeatAtUtc;

  private IPrinterSession? _openSession;
  private DateTimeOffset? _reconnectNotBeforeUtc;
  private int _unansweredHeartbeats;

  public PrinterWorker(Printer printer,
                       IReadOnlyCollection<Guid> servedStationIds,
                       IPrinterDriver driver,
                       IPrinterWorkerDataAccess dataAccess,
                       IPrintCallbacks callbacks,
                       EscPosSlipRenderer renderer,
                       PrinterWorkerDomainServices domainServices,
                       TimeProvider timeProvider,
                       AppLanguage language,
                       ILogger<PrinterWorker> logger)
  {
    _language = language;
    this.Printer = printer;
    _driver = driver;
    _dataAccess = dataAccess;
    _callbacks = callbacks;
    _renderer = renderer;
    _domainServices = domainServices;
    _timeProvider = timeProvider;
    _logger = logger;
    ServedStationIds = [.. servedStationIds];
    _lastHeartbeatAtUtc = timeProvider.GetUtcNow();
  }

  public Guid[] ServedStationIds { get; }

  public Printer Printer { get; }

  public bool IsFaulty => _circuitBreaker.IsTripped;

  public bool HasOpenSession => _openSession is not null;

  public IReadOnlyList<Guid> PendingPrintJobIds
  {
    get
    {
      lock (_guard)
      {
        return [.. _pending];
      }
    }
  }

  public IReadOnlyList<Guid> PendingTestPrintJobIds
  {
    get
    {
      lock (_guard)
      {
        return [.. _pendingTestPrints.Select(_pending => _pending.PrintJobId)];
      }
    }
  }

  public void Enqueue(Guid printJobId)
  {
    lock (_guard)
    {
      if (!_pending.Contains(printJobId))
      {
        _pending.Add(printJobId);
      }
    }
  }

  public void EnqueueTestPrint(Guid printJobId)
  {
    lock (_guard)
    {
      if (!_pendingTestPrints.Any(candidate => candidate.PrintJobId == printJobId))
      {
        _pendingTestPrints.Add(new(printJobId));
      }
    }
  }

  public TimeSpan NextReconnectDelay()
  {
    return _reconnectBackoff.Next();
  }

  public void AcceptStatusSnapshot(PrinterStatusSnapshot snapshot)
  {
    _cachedStatus = snapshot;
  }

  public async Task RecoverAtStartupAsync(CancellationToken cancellationToken)
  {
    await _dataAccess.MarkSendingJobsUnknownAsync(ServedStationIds, cancellationToken);
    IReadOnlyList<Guid> recoverable =
      await _dataAccess.LoadRecoverablePrintJobIdsAsync(ServedStationIds, cancellationToken);
    foreach (var printJobId in recoverable)
    {
      Enqueue(printJobId);
    }
  }

  public async Task<IReadOnlyList<Guid>> ReconnectAsync(CancellationToken cancellationToken)
  {
    _circuitBreaker.Reset();
    _reconnectBackoff.Reset();
    _reconnectNotBeforeUtc = null;
    await CloseSessionAsync();
    await _dataAccess.ClearFaultyAtEndpointAsync(ServedStationIds, cancellationToken);

    await PushPrinterStatusAsync(CurrentStatusOrOffline(), false, cancellationToken);

    return ServedStationIds;
  }

  public async Task RunAsync(CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        await RunOnceAsync(cancellationToken);
        await HeartbeatAsync(cancellationToken);
        await Task.Delay(RemainingBackoff(), cancellationToken);
      }
      catch (OperationCanceledException)
      {
        return;
      }
    }

    await CloseSessionAsync();
  }

  private TimeSpan RemainingBackoff()
  {
    var idle = TimeSpan.FromMilliseconds(50);
    if (_reconnectNotBeforeUtc is null)
    {
      return idle;
    }

    var remaining = _reconnectNotBeforeUtc.Value - _timeProvider.GetUtcNow();
    return remaining > idle ? remaining : idle;
  }

  public async Task HeartbeatAsync(CancellationToken cancellationToken)
  {
    if (_openSession is null || _timeProvider.GetUtcNow() - _lastHeartbeatAtUtc < _driver.HeartbeatInterval)
    {
      return;
    }

    _lastHeartbeatAtUtc = _timeProvider.GetUtcNow();
    try
    {
      var snapshot = await _openSession.QueryStatusAsync(cancellationToken);
      _unansweredHeartbeats = 0;
      AcceptStatusSnapshot(snapshot);
    }
    catch (Exception error) when (error is IOException or InvalidOperationException or ObjectDisposedException)
    {
      _unansweredHeartbeats++;
      _logger.LogWarning(error, "Heartbeat number {Unanswered} went unanswered at printer {PrinterName}.", _unansweredHeartbeats, Printer.Name);

      if (_unansweredHeartbeats >= 2)
      {
        await CloseSessionAsync();
        _unansweredHeartbeats = 0;
        await PushOfflineAsync("Two consecutive heartbeats went unanswered.", cancellationToken);
      }
    }
  }

  public async Task RunOnceAsync(CancellationToken cancellationToken)
  {
    if (_circuitBreaker.IsTripped)
    {
      return;
    }

    if (_reconnectNotBeforeUtc is not null && _timeProvider.GetUtcNow() < _reconnectNotBeforeUtc.Value)
    {
      return;
    }

    if (await TryRunTestPrintAsync(cancellationToken))
    {
      return;
    }

    Guid? head = await NextPrintJobAsync(cancellationToken);
    if (head is null)
    {
      return;
    }

    var job = await _dataAccess.LoadPrintJobAsync(head.Value, cancellationToken);
    var claim = await _dataAccess.TryClaimAsync(head.Value, cancellationToken);

    switch (claim.Outcome)
    {
      case ClaimOutcome.NoLongerWaiting:
        await EndAsHandledOnPaperAsync(job, cancellationToken);
        return;
      case ClaimOutcome.PrinterFaulty:
        _logger.LogInformation("Print job {PrintJobId} was left unclaimed because its printer is faulty.", head.Value);
        return;
      case ClaimOutcome.Claimed:
        break;
      default:
        new Never().OfType<bool>(claim.Outcome);
        return;
    }

    var session = await EnsureSessionAsync(cancellationToken);
    if (session is null)
    {
      await FinishAsync(job,
                        claim,
                        new(PrintOutcome.Unreachable, 0, CurrentStatusOrOffline(), "The printer could not be reached."),
                        cancellationToken);
      return;
    }

    var preflight = await PreflightAsync(session, cancellationToken);
    if (preflight.IsBlocking)
    {
      await FinishAsync(job,
                        claim,
                        new(preflight.BlockingReason == PrintFailureReason.PrinterError ? PrintOutcome.PrinterError : PrintOutcome.Blocked,
                            0,
                            preflight.Snapshot,
                            $"Pre-flight refused the job: {preflight.BlockingReason}."),
                        cancellationToken);
      return;
    }

    var slip = Render(job);
    var printerJobId = await _dataAccess.AllocatePrinterJobIdAsync(cancellationToken);

    PrintPayload payload = new(printerJobId,
                               slip.Bytes,
                               slip.RenderedText,
                               job.CopyNumber,
                               job.StationOrderNumber,
                               job.StationId,
                               job.StationName,
                               false);

    PrintDispatchResult dispatch;
    try
    {
      dispatch = await session.SendJobAsync(payload, cancellationToken);
    }
    catch (Exception error) when (error is IOException or InvalidOperationException or ObjectDisposedException)
    {
      _logger.LogWarning(error, "Sending print job {PrintJobId} failed at printer {PrinterName}.", job.PrintJobId, Printer.Name);
      await CloseSessionAsync();
      dispatch = new(PrintOutcome.SocketDropped, 0, CurrentStatusOrOffline(), error.Message);
    }

    await FinishAsync(job, claim, dispatch, cancellationToken);
  }

  private async Task<bool> TryRunTestPrintAsync(CancellationToken cancellationToken)
  {
    PendingTestPrint _pending;
    lock (_guard)
    {
      if (_pendingTestPrints.Count == 0)
      {
        return false;
      }

      _pending = _pendingTestPrints[0];
      _pendingTestPrints.RemoveAt(0);
    }

    var session = await EnsureSessionAsync(cancellationToken);
    if (session is null)
    {
      _logger.LogWarning("The test slip for printer {PrinterName} was not sent, because the printer could not be reached.",
                        Printer.Name);
      return true;
    }

    var slip = _renderer.RenderTestSlip(new(Printer.Name,
                                           _language.Current,
                                           _timeProvider.GetUtcNow(),
                                           TimeZoneInfo.Local));

    var printerJobId = await _dataAccess.AllocatePrinterJobIdAsync(cancellationToken);
    var dispatch = await session.SendJobAsync(new(printerJobId, slip.Bytes, slip.RenderedText, 0, 0, Printer.Id, Printer.Name, true),
                                              cancellationToken);

    await _dataAccess.WritePrinterStatusAsync(Printer.Id, dispatch.StatusAtEnd, cancellationToken);
    await PushPrinterStatusAsync(dispatch.StatusAtEnd, false, cancellationToken);
    return true;
  }

  private async Task<Guid?> NextPrintJobAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<Guid> snapshot = PendingPrintJobIds;
    if (snapshot.Count == 0)
    {
      return null;
    }

    IReadOnlyList<Guid> ordered = await _dataAccess.OrderQueueAsync(snapshot, cancellationToken);
    foreach (var candidate in ordered)
    {
      if (snapshot.Contains(candidate))
      {
        return candidate;
      }
    }

    return snapshot[0];
  }

  private RenderedSlip Render(PrintJobLoadResult job)
  {
    SlipRenderRequest request = new(job.StationName,
                                    _language.Current,
                                    job.StationOrderNumber,
                                    job.GlobalOrderNumber,
                                    job.TableName,
                                    job.StaffMemberName,
                                    new(DateTime.SpecifyKind(job.OrderCreatedAtUtc, DateTimeKind.Utc)),
                                    TimeZoneInfo.Local,
                                    [
                                      .. _lineCollapser
                                        .Collapse(job.Items, item => item.ItemName, item => item.ItemNote)
                                        .Select(collapsed => new SlipLine(collapsed.Quantity,
                                                                          collapsed.Line.ItemName,
                                                                          collapsed.Line.ItemNote))
                                    ],
                                    job.OrderNote,
                                    job.AlsoGoesToStationNames);

    return job.CopyNumber > 0
             ? _renderer.RenderCopySlip(request, job.CopyNumber, _timeProvider.GetUtcNow(), TimeZoneInfo.Local)
             : _renderer.RenderInitialSlip(request);
  }

  private async Task<IPrinterSession?> EnsureSessionAsync(CancellationToken cancellationToken)
  {
    if (_openSession is not null)
    {
      return _openSession;
    }

    try
    {
      _openSession = await _driver.ConnectAsync(Printer, cancellationToken);
      _reconnectBackoff.Reset();
      _reconnectNotBeforeUtc = null;
      _unansweredHeartbeats = 0;
      _lastHeartbeatAtUtc = _timeProvider.GetUtcNow();
      return _openSession;
    }
    catch (Exception error) when (error is PrinterUnreachableException or IOException or InvalidOperationException)
    {
      var retryIn = _reconnectBackoff.Next();
      _reconnectNotBeforeUtc = _timeProvider.GetUtcNow() + retryIn;
      _logger.LogWarning(error, "Connecting to printer {PrinterName} failed; retrying in {RetryIn}.", Printer.Name, retryIn);
      await PushOfflineAsync(error.Message, cancellationToken);
      return null;
    }
  }

  private async Task<PreflightResult> PreflightAsync(IPrinterSession session, CancellationToken cancellationToken)
  {
    PrinterStatusSnapshot snapshot;
    if (_cachedStatus is not null && _timeProvider.GetUtcNow() - _cachedStatus.ObservedAt < _driver.HeartbeatInterval)
    {
      snapshot = _cachedStatus;
    }
    else
    {
      snapshot = await session.QueryStatusAsync(cancellationToken);
    }

    PrintFailureReason? blockingReason = snapshot switch
                                         {
                                           { IsPaperEnd: true } => PrintFailureReason.PaperEnd,
                                           { IsCoverOpen: true } => PrintFailureReason.CoverOpen,
                                           { IsInErrorState: true } => PrintFailureReason.PrinterError,
                                           _ => null
                                         };

    return new(blockingReason is not null, snapshot, blockingReason);
  }

  private async Task EndAsHandledOnPaperAsync(PrintJobLoadResult job, CancellationToken cancellationToken)
  {
    Remove(job.PrintJobId);

    await _callbacks.OnPrintJobStatusChangedAsync(job.OrderId,
                                                 job.StationOrderId,
                                                 job.Status,
                                                 null,
                                                 cancellationToken);
    await PushOrderProjectionAsync(job.OrderId, cancellationToken);
  }

  private async Task FinishAsync(PrintJobLoadResult job,
                                 ClaimResult claim,
                                 PrintDispatchResult dispatch,
                                 CancellationToken cancellationToken)
  {
    var mapping = MapOutcome(dispatch, job.PrintJobId);

    if (!_domainServices.PrintJobStateMachine.CanTransition(PrintJobStatus.Sending, mapping.JobStatus))
    {
      throw new InvalidOperationException($"The print job state machine refuses Sending to {mapping.JobStatus}, which the job sequence requires for print job {job.PrintJobId}.");
    }

    var applied = await _dataAccess.ApplyOutcomeAsync(new()
                                                     {
                                                       PrintJobId = claim.PrintJobId,
                                                       Outcome = dispatch.Outcome,
                                                       BytesWritten = dispatch.BytesWritten,
                                                       StatusAtEnd = dispatch.StatusAtEnd,
                                                       JobStatus = mapping.JobStatus,
                                                       FailureReason = null
                                                     },
                                                     cancellationToken);

    await _dataAccess.WritePrinterStatusAsync(Printer.Id, dispatch.StatusAtEnd, cancellationToken);
    await _callbacks.OnPrintJobStatusChangedAsync(job.OrderId,
                                                 job.StationOrderId,
                                                 applied.PrintJobStatus,
                                                 null,
                                                 cancellationToken);
    await PushPrinterStatusAsync(dispatch.StatusAtEnd, false, cancellationToken);

    if (mapping.JobStatus == PrintJobStatus.Unknown)
    {
      await ReQueryAfterUnknownAsync(cancellationToken);
    }

    var stillWaiting = !applied.WasHandledOnPaper
                       && mapping.ShouldRetryAutomatically
                       && !await GiveUpAsync(job, claim, dispatch, mapping, cancellationToken);

    if (stillWaiting)
    {
      Enqueue(job.PrintJobId);
    }
    else
    {
      Remove(job.PrintJobId);
    }

    await PushOrderProjectionAsync(job.OrderId, cancellationToken);
    await TripBreakerIfNeededAsync(dispatch, mapping, cancellationToken);
  }

  private async Task ReQueryAfterUnknownAsync(CancellationToken cancellationToken)
  {
    if (_openSession is null)
    {
      return;
    }

    try
    {
      var reQueried = await _openSession.QueryStatusAsync(cancellationToken);
      AcceptStatusSnapshot(reQueried);
      await _dataAccess.WritePrinterStatusAsync(Printer.Id, reQueried, cancellationToken);
      await PushPrinterStatusAsync(reQueried, false, cancellationToken);
    }
    catch (Exception error) when (error is IOException or InvalidOperationException or ObjectDisposedException)
    {
      _logger.LogWarning(error, "The status re-query after an unknown outcome failed at printer {PrinterName}.", Printer.Name);
      await CloseSessionAsync();
    }
  }

  private PrintOutcomeMapping MapOutcome(PrintDispatchResult dispatch, Guid printJobId)
  {
    try
    {
      return _domainServices.RetryPolicy.Map(dispatch.Outcome, dispatch.BytesWritten);
    }
    catch (InvalidOperationException error)
    {
      _logger.LogError(error,
                      "The retry policy has no row for outcome {Outcome} with {BytesWritten} bytes written, so print job {PrintJobId} is recorded as unknown.",
                      dispatch.Outcome,
                      dispatch.BytesWritten,
                      printJobId);

      return new()
             {
               JobStatus = PrintJobStatus.Unknown,
               ShouldRetryAutomatically = false
             };
    }
  }

  private async Task<bool> GiveUpAsync(PrintJobLoadResult job,
                                       ClaimResult claim,
                                       PrintDispatchResult dispatch,
                                       PrintOutcomeMapping mapping,
                                       CancellationToken cancellationToken)
  {
    IReadOnlyList<SuspensionPeriod> suspensions =
      await _dataAccess.LoadSuspensionPeriodsAsync(job.StationId, cancellationToken);

    var evaluation = _domainServices.GiveUpWindowCalculator.Evaluate(job.CreatedAtUtc,
                                                                    _timeProvider.GetUtcNow().UtcDateTime,
                                                                    mapping.JobStatus,
                                                                    suspensions);

    if (!evaluation.HasReachedGiveUpWindow && !evaluation.HasReachedOuterBound)
    {
      return false;
    }

    var reason = FailureReasonFor(dispatch);
    await _dataAccess.FailPrintJobAsync(claim.PrintJobId, reason, cancellationToken);
    await _callbacks.OnPrintJobStatusChangedAsync(job.OrderId,
                                                 job.StationOrderId,
                                                 PrintJobStatus.Failed,
                                                 reason,
                                                 cancellationToken);

    return true;
  }

  private PrintFailureReason FailureReasonFor(PrintDispatchResult dispatch)
  {
    return dispatch.Outcome switch
           {
             PrintOutcome.Blocked => dispatch.StatusAtEnd.IsCoverOpen ? PrintFailureReason.CoverOpen : PrintFailureReason.PaperEnd,
             PrintOutcome.PrinterError => PrintFailureReason.PrinterError,
             PrintOutcome.Unreachable => PrintFailureReason.Unreachable,
             PrintOutcome.SocketDropped => PrintFailureReason.SocketDropped,
             PrintOutcome.Timeout => PrintFailureReason.Timeout,
             PrintOutcome.Confirmed => PrintFailureReason.PrinterError,
             _ => new Never().OfType<PrintFailureReason>(dispatch.Outcome)
           };
  }

  private async Task TripBreakerIfNeededAsync(PrintDispatchResult dispatch,
                                              PrintOutcomeMapping mapping,
                                              CancellationToken cancellationToken)
  {
    if (!_circuitBreaker.RecordOutcome(dispatch.Outcome, mapping.JobStatus))
    {
      return;
    }

    _logger.LogError("The printer {PrinterName} stopped making sense and its circuit breaker tripped.", Printer.Name);
    await _dataAccess.FailAllWaitingAtEndpointAsync(ServedStationIds, PrintFailureReason.StationFaulty, cancellationToken);

    lock (_guard)
    {
      _pending.Clear();
    }

    await PushPrinterStatusAsync(dispatch.StatusAtEnd, true, cancellationToken);
  }

  private async Task PushOrderProjectionAsync(Guid orderId, CancellationToken cancellationToken)
  {
    var statuses = await _dataAccess.LoadOrderPrintJobStatusesAsync(orderId, cancellationToken);

    await _callbacks.OnOrderStatusChangedAsync(orderId, statuses.CurrentStatus, cancellationToken);
  }

  private async Task PushOfflineAsync(string detail, CancellationToken cancellationToken)
  {
    PrinterStatusSnapshot offline = new(false, false, false, false, false, detail, _timeProvider.GetUtcNow());
    _cachedStatus = null;

    await _dataAccess.WritePrinterStatusAsync(Printer.Id, offline, cancellationToken);
    await PushPrinterStatusAsync(offline, false, cancellationToken);
  }

  private async Task PushPrinterStatusAsync(PrinterStatusSnapshot snapshot,
                                            bool isFaulty,
                                            CancellationToken cancellationToken)
  {
    var waiting = 0;
    foreach (var stationId in ServedStationIds)
    {
      waiting += await _dataAccess.CountWaitingPrintJobsAsync(stationId, cancellationToken);
    }

    await _callbacks.OnPrinterStatusChangedAsync(Printer.Id,
                                                ServedStationIds,
                                                snapshot,
                                                isFaulty,
                                                waiting,
                                                cancellationToken);
  }

  private PrinterStatusSnapshot CurrentStatusOrOffline()
  {
    return _cachedStatus ?? new PrinterStatusSnapshot(false, false, false, false, false, "No status has been observed yet.", _timeProvider.GetUtcNow());
  }

  private void Remove(Guid printJobId)
  {
    lock (_guard)
    {
      _pending.Remove(printJobId);
    }
  }

  private async Task CloseSessionAsync()
  {
    if (_openSession is null)
    {
      return;
    }

    var closing = _openSession;
    _openSession = null;
    await closing.DisposeAsync();
  }
}
