using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class PrintJobStateMachineTest
{
    private PrintJobStateMachine _stateMachine = new();

    [SetUp]
    public void SetUp()
    {
        _stateMachine = new PrintJobStateMachine();
    }

    private HashSet<(PrintJobStatus From, PrintJobStatus To)> DrawnEdges()
    {
        return
        [
            (PrintJobStatus.AwaitingEcho, PrintJobStatus.Confirmed),
            (PrintJobStatus.AwaitingEcho, PrintJobStatus.Unknown),
            (PrintJobStatus.Blocked, PrintJobStatus.Failed),
            (PrintJobStatus.Blocked, PrintJobStatus.Queued),
            (PrintJobStatus.PreflightCheck, PrintJobStatus.Blocked),
            (PrintJobStatus.PreflightCheck, PrintJobStatus.Queued),
            (PrintJobStatus.PreflightCheck, PrintJobStatus.Sending),
            (PrintJobStatus.Queued, PrintJobStatus.Failed),
            (PrintJobStatus.Queued, PrintJobStatus.PreflightCheck),
            (PrintJobStatus.Sending, PrintJobStatus.AwaitingEcho),
            (PrintJobStatus.Sending, PrintJobStatus.Queued),
            (PrintJobStatus.Sending, PrintJobStatus.Unknown),
            (PrintJobStatus.Unknown, PrintJobStatus.ResolvedMissing),
            (PrintJobStatus.Unknown, PrintJobStatus.ResolvedPrinted),
        ];
    }

    [Test]
    public void CanTransition_QueuedToPreflightCheck_WorkerPickedItUpAndTheConnectionIsOpen_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(PrintJobStatus.Queued, PrintJobStatus.PreflightCheck),
            Is.True);
    }

    [Test]
    public void CanTransition_PreflightCheckToBlocked_TheStatusSaysPaperEndOrCoverOpen_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(PrintJobStatus.PreflightCheck, PrintJobStatus.Blocked),
            Is.True);
    }

    [Test]
    public void CanTransition_BlockedToQueued_TheStatusCleared_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(PrintJobStatus.Blocked, PrintJobStatus.Queued),
            Is.True);
    }

    [Test]
    public void CanTransition_PreflightCheckToQueued_TheConnectionWasLostBeforeAnyByte_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(PrintJobStatus.PreflightCheck, PrintJobStatus.Queued),
            Is.True);
    }

    [Test]
    public void CanTransition_PreflightCheckToSending_TheStatusIsClean_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(PrintJobStatus.PreflightCheck, PrintJobStatus.Sending),
            Is.True);
    }

    [Test]
    public void CanTransition_SendingToAwaitingEcho_EveryByteWasWritten_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(PrintJobStatus.Sending, PrintJobStatus.AwaitingEcho),
            Is.True);
    }

    [Test]
    public void CanTransition_SendingToQueued_TheSocketDroppedBeforeTheFirstByte_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(PrintJobStatus.Sending, PrintJobStatus.Queued),
            Is.True);
    }

    [Test]
    public void CanTransition_SendingToUnknown_TheSocketDroppedPartWayThroughTheWrite_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(PrintJobStatus.Sending, PrintJobStatus.Unknown),
            Is.True);
    }

    [Test]
    public void CanTransition_AwaitingEchoToConfirmed_ThePrinterReturnedTheProcessId_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(PrintJobStatus.AwaitingEcho, PrintJobStatus.Confirmed),
            Is.True);
    }

    [Test]
    public void CanTransition_AwaitingEchoToUnknown_TheSocketDroppedOrTheJobTimedOut_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(PrintJobStatus.AwaitingEcho, PrintJobStatus.Unknown),
            Is.True);
    }

    [Test]
    public void CanTransition_QueuedToFailed_TheGiveUpWindowExpiredOrTheStationIsGone_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(PrintJobStatus.Queued, PrintJobStatus.Failed),
            Is.True);
    }

    [Test]
    public void CanTransition_BlockedToFailed_TheGiveUpWindowExpiredOrTheStationIsGone_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(PrintJobStatus.Blocked, PrintJobStatus.Failed),
            Is.True);
    }

    [Test]
    public void CanTransition_UnknownToResolvedPrinted_AHumanAnsweredTheSlipIsThere_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(PrintJobStatus.Unknown, PrintJobStatus.ResolvedPrinted),
            Is.True);
    }

    [Test]
    public void CanTransition_UnknownToResolvedMissing_AHumanAnsweredTheSlipIsMissing_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(PrintJobStatus.Unknown, PrintJobStatus.ResolvedMissing),
            Is.True);
    }

    [Test]
    public void CanTransition_EveryPairThatIsNotDrawn_IsRefused()
    {
        HashSet<(PrintJobStatus From, PrintJobStatus To)> drawnEdges = DrawnEdges();
        PrintJobStatus[] allStatuses = Enum.GetValues<PrintJobStatus>();
        int refusedPairs = 0;

        foreach (PrintJobStatus from in allStatuses)
        {
            foreach (PrintJobStatus to in allStatuses)
            {
                if (drawnEdges.Contains((from, to)))
                {
                    continue;
                }

                Assert.That(
                    _stateMachine.CanTransition(from, to),
                    Is.False,
                    $"{from} to {to} is not drawn in the diagram");
                refusedPairs++;
            }
        }

        Assert.That(refusedPairs, Is.EqualTo(100 - drawnEdges.Count));
    }

    [Test]
    public void CanTransition_EveryDrawnEdge_IsAllowed()
    {
        foreach ((PrintJobStatus From, PrintJobStatus To) edge in DrawnEdges())
        {
            Assert.That(
                _stateMachine.CanTransition(edge.From, edge.To),
                Is.True,
                $"{edge.From} to {edge.To}");
        }
    }

    [Test]
    public void CanTransition_OutOfATerminalState_IsAlwaysRefused()
    {
        PrintJobStatus[] terminalStatuses =
        [
            PrintJobStatus.Confirmed,
            PrintJobStatus.Failed,
            PrintJobStatus.ResolvedPrinted,
            PrintJobStatus.ResolvedMissing,
        ];

        foreach (PrintJobStatus terminal in terminalStatuses)
        {
            foreach (PrintJobStatus to in Enum.GetValues<PrintJobStatus>())
            {
                Assert.That(_stateMachine.CanTransition(terminal, to), Is.False, $"{terminal} to {to}");
            }
        }
    }

    [Test]
    public void CanTransition_FromEveryJobStatusReachedWithBytesWritten_NeverLeadsBackToARetryableState()
    {
        RetryPolicy retryPolicy = new();
        PrintAttemptOutcome[] outcomesTheTableDefinesWithBytesWritten =
        [
            PrintAttemptOutcome.Confirmed,
            PrintAttemptOutcome.SocketDropped,
            PrintAttemptOutcome.Timeout,
            PrintAttemptOutcome.PrinterError,
        ];

        foreach (PrintAttemptOutcome outcome in outcomesTheTableDefinesWithBytesWritten)
        {
            foreach (TransportKind transportKind in Enum.GetValues<TransportKind>())
            {
                PrintJobStatus reachedStatus = retryPolicy.Map(outcome, transportKind, 128).JobStatus;

                Assert.Multiple(() =>
                {
                    Assert.That(
                        _stateMachine.CanTransition(reachedStatus, PrintJobStatus.Queued),
                        Is.False,
                        $"{outcome} on {transportKind} reached {reachedStatus}");
                    Assert.That(
                        _stateMachine.CanTransition(reachedStatus, PrintJobStatus.PreflightCheck),
                        Is.False,
                        $"{outcome} on {transportKind} reached {reachedStatus}");
                });
            }
        }
    }
}
