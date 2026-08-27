using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class TicketStateMachineTest
{
    private TicketStateMachine _stateMachine = new();

    [SetUp]
    public void SetUp()
    {
        _stateMachine = new TicketStateMachine();
    }

    private HashSet<(LocationTicketStatus From, LocationTicketStatus To)> DrawnEdges()
    {
        return
        [
            (LocationTicketStatus.Blocked, LocationTicketStatus.Failed),
            (LocationTicketStatus.Blocked, LocationTicketStatus.HandledOnPaper),
            (LocationTicketStatus.Blocked, LocationTicketStatus.Queued),
            (LocationTicketStatus.Failed, LocationTicketStatus.HandledOnPaper),
            (LocationTicketStatus.Failed, LocationTicketStatus.Queued),
            (LocationTicketStatus.Printed, LocationTicketStatus.Queued),
            (LocationTicketStatus.PrintedOnTestPrinter, LocationTicketStatus.Queued),
            (LocationTicketStatus.Printing, LocationTicketStatus.Printed),
            (LocationTicketStatus.Printing, LocationTicketStatus.PrintedOnTestPrinter),
            (LocationTicketStatus.Printing, LocationTicketStatus.Queued),
            (LocationTicketStatus.Printing, LocationTicketStatus.Blocked),
            (LocationTicketStatus.Printing, LocationTicketStatus.Unknown),
            (LocationTicketStatus.Queued, LocationTicketStatus.Blocked),
            (LocationTicketStatus.Queued, LocationTicketStatus.Failed),
            (LocationTicketStatus.Queued, LocationTicketStatus.HandledOnPaper),
            (LocationTicketStatus.Queued, LocationTicketStatus.Printing),
            (LocationTicketStatus.Unknown, LocationTicketStatus.HandledOnPaper),
            (LocationTicketStatus.Unknown, LocationTicketStatus.Printed),
            (LocationTicketStatus.Unknown, LocationTicketStatus.Queued),
        ];
    }

    [Test]
    public void CanTransition_QueuedToPrinting_WorkerClaimedTheJob_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Queued, LocationTicketStatus.Printing),
            Is.True);
    }

    [Test]
    public void CanTransition_PrintingToPrinted_PrinterEchoedTheProcessId_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Printing, LocationTicketStatus.Printed),
            Is.True);
    }

    [Test]
    public void CanTransition_PrintingToPrintedOnTestPrinter_TheTestPrinterRenderedIt_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Printing, LocationTicketStatus.PrintedOnTestPrinter),
            Is.True);
    }

    [Test]
    public void CanTransition_PrintingToUnknown_TheSocketDroppedAfterBytesWereWritten_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Printing, LocationTicketStatus.Unknown),
            Is.True);
    }

    [Test]
    public void CanTransition_PrintingToQueued_TheAttemptFailedBeforeAnyByte_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Printing, LocationTicketStatus.Queued),
            Is.True);
    }

    [Test]
    public void CanTransition_QueuedToBlocked_PreflightSaysPaperEndOrCoverOpen_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Queued, LocationTicketStatus.Blocked),
            Is.True);
    }

    [Test]
    public void CanTransition_BlockedToQueued_ThePrinterReportsItIsReadyAgain_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Blocked, LocationTicketStatus.Queued),
            Is.True);
    }

    [Test]
    public void CanTransition_QueuedToFailed_TheGiveUpWindowOrOuterBoundEnded_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Queued, LocationTicketStatus.Failed),
            Is.True);
    }

    [Test]
    public void CanTransition_BlockedToFailed_TheGiveUpWindowOrOuterBoundEnded_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Blocked, LocationTicketStatus.Failed),
            Is.True);
    }

    [Test]
    public void CanTransition_UnknownToPrinted_AHumanAnsweredTheSlipIsOnThePile_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Unknown, LocationTicketStatus.Printed),
            Is.True);
    }

    [Test]
    public void CanTransition_UnknownToQueued_AHumanAnsweredTheSlipIsMissing_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Unknown, LocationTicketStatus.Queued),
            Is.True);
    }

    [Test]
    public void CanTransition_FailedToQueued_AHumanAskedForAReprint_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Failed, LocationTicketStatus.Queued),
            Is.True);
    }

    [Test]
    public void CanTransition_FailedToHandledOnPaper_StationStaffAcknowledgedIt_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Failed, LocationTicketStatus.HandledOnPaper),
            Is.True);
    }

    [Test]
    public void CanTransition_UnknownToHandledOnPaper_StationStaffAcknowledgedIt_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Unknown, LocationTicketStatus.HandledOnPaper),
            Is.True);
    }

    [Test]
    public void CanTransition_BlockedToHandledOnPaper_AcknowledgedAtAStationThatCannotPrint_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Blocked, LocationTicketStatus.HandledOnPaper),
            Is.True);
    }

    [Test]
    public void CanTransition_QueuedToHandledOnPaper_AcknowledgedAtAStationThatCannotPrint_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Queued, LocationTicketStatus.HandledOnPaper),
            Is.True);
    }

    [Test]
    public void CanTransition_PrintedToQueued_AHumanAskedForAReprint_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Printed, LocationTicketStatus.Queued),
            Is.True);
    }

    [Test]
    public void CanTransition_PrintedOnTestPrinterToQueued_AHumanAskedForAReprint_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.PrintedOnTestPrinter, LocationTicketStatus.Queued),
            Is.True);
    }

    [Test]
    public void CanTransition_PrintingToBlocked_PreflightFoundPaperEndOrCoverOpenAfterTheClaim_IsAllowed()
    {
        Assert.That(
            _stateMachine.CanTransition(LocationTicketStatus.Printing, LocationTicketStatus.Blocked),
            Is.True);
    }

    [Test]
    public void CanTransition_EveryPairThatIsNotDrawn_IsRefused()
    {
        HashSet<(LocationTicketStatus From, LocationTicketStatus To)> drawnEdges = DrawnEdges();
        LocationTicketStatus[] allStatuses = Enum.GetValues<LocationTicketStatus>();
        int refusedPairs = 0;

        foreach (LocationTicketStatus from in allStatuses)
        {
            foreach (LocationTicketStatus to in allStatuses)
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

        Assert.That(refusedPairs, Is.EqualTo(64 - drawnEdges.Count));
    }

    [Test]
    public void CanTransition_EveryDrawnEdge_IsAllowed()
    {
        foreach ((LocationTicketStatus From, LocationTicketStatus To) edge in DrawnEdges())
        {
            Assert.That(
                _stateMachine.CanTransition(edge.From, edge.To),
                Is.True,
                $"{edge.From} to {edge.To}");
        }
    }

    [Test]
    public void CanTransition_OutOfATerminalState_IsRefusedExceptForAReprint()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_stateMachine.CanTransition(LocationTicketStatus.Printed, LocationTicketStatus.Queued), Is.True);
            Assert.That(_stateMachine.CanTransition(LocationTicketStatus.Printed, LocationTicketStatus.Printing), Is.False);
            Assert.That(_stateMachine.CanTransition(LocationTicketStatus.HandledOnPaper, LocationTicketStatus.Queued), Is.False);
            Assert.That(_stateMachine.CanTransition(LocationTicketStatus.HandledOnPaper, LocationTicketStatus.Printing), Is.False);
        });
    }
}
