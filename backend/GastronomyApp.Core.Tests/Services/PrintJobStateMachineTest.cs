using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class PrintJobStateMachineTest
{

  [SetUp]
  public void SetUp()
  {
    _stateMachine = new();
  }

  private PrintJobStateMachine _stateMachine = new();

  private HashSet<(PrintJobStatus From, PrintJobStatus To)> DrawnEdges()
  {
    return
    [
      (PrintJobStatus.Blocked, PrintJobStatus.Failed),
      (PrintJobStatus.Blocked, PrintJobStatus.HandledOnPaper),
      (PrintJobStatus.Blocked, PrintJobStatus.Queued),
      (PrintJobStatus.Failed, PrintJobStatus.HandledOnPaper),
      (PrintJobStatus.Sending, PrintJobStatus.Printed),
      (PrintJobStatus.Sending, PrintJobStatus.Queued),
      (PrintJobStatus.Sending, PrintJobStatus.Blocked),
      (PrintJobStatus.Sending, PrintJobStatus.Unknown),
      (PrintJobStatus.Queued, PrintJobStatus.Blocked),
      (PrintJobStatus.Queued, PrintJobStatus.Failed),
      (PrintJobStatus.Queued, PrintJobStatus.HandledOnPaper),
      (PrintJobStatus.Queued, PrintJobStatus.Sending),
      (PrintJobStatus.Unknown, PrintJobStatus.HandledOnPaper),
      (PrintJobStatus.Unknown, PrintJobStatus.Printed),
      (PrintJobStatus.Unknown, PrintJobStatus.Queued)
    ];
  }

  [Test]
  public void CanTransition_QueuedToPrinting_WorkerClaimedTheJob_IsAllowed()
  {
    Assert.That(_stateMachine.CanTransition(PrintJobStatus.Queued, PrintJobStatus.Sending),
                Is.True);
  }

  [Test]
  public void CanTransition_PrintingToPrinted_PrinterEchoedTheProcessId_IsAllowed()
  {
    Assert.That(_stateMachine.CanTransition(PrintJobStatus.Sending, PrintJobStatus.Printed),
                Is.True);
  }


  [Test]
  public void CanTransition_PrintingToUnknown_TheSocketDroppedAfterBytesWereWritten_IsAllowed()
  {
    Assert.That(_stateMachine.CanTransition(PrintJobStatus.Sending, PrintJobStatus.Unknown),
                Is.True);
  }

  [Test]
  public void CanTransition_PrintingToQueued_TheAttemptFailedBeforeAnyByte_IsAllowed()
  {
    Assert.That(_stateMachine.CanTransition(PrintJobStatus.Sending, PrintJobStatus.Queued),
                Is.True);
  }

  [Test]
  public void CanTransition_QueuedToBlocked_PreflightSaysPaperEndOrCoverOpen_IsAllowed()
  {
    Assert.That(_stateMachine.CanTransition(PrintJobStatus.Queued, PrintJobStatus.Blocked),
                Is.True);
  }

  [Test]
  public void CanTransition_BlockedToQueued_ThePrinterReportsItIsReadyAgain_IsAllowed()
  {
    Assert.That(_stateMachine.CanTransition(PrintJobStatus.Blocked, PrintJobStatus.Queued),
                Is.True);
  }

  [Test]
  public void CanTransition_QueuedToFailed_TheGiveUpWindowOrOuterBoundEnded_IsAllowed()
  {
    Assert.That(_stateMachine.CanTransition(PrintJobStatus.Queued, PrintJobStatus.Failed),
                Is.True);
  }

  [Test]
  public void CanTransition_BlockedToFailed_TheGiveUpWindowOrOuterBoundEnded_IsAllowed()
  {
    Assert.That(_stateMachine.CanTransition(PrintJobStatus.Blocked, PrintJobStatus.Failed),
                Is.True);
  }

  [Test]
  public void CanTransition_UnknownToPrinted_AHumanAnsweredTheSlipIsOnThePile_IsAllowed()
  {
    Assert.That(_stateMachine.CanTransition(PrintJobStatus.Unknown, PrintJobStatus.Printed),
                Is.True);
  }

  [Test]
  public void CanTransition_UnknownToQueued_AHumanAnsweredTheSlipIsMissing_IsAllowed()
  {
    Assert.That(_stateMachine.CanTransition(PrintJobStatus.Unknown, PrintJobStatus.Queued),
                Is.True);
  }


  [Test]
  public void CanTransition_FailedToHandledOnPaper_StationStaffAcknowledgedIt_IsAllowed()
  {
    Assert.That(_stateMachine.CanTransition(PrintJobStatus.Failed, PrintJobStatus.HandledOnPaper),
                Is.True);
  }

  [Test]
  public void CanTransition_UnknownToHandledOnPaper_StationStaffAcknowledgedIt_IsAllowed()
  {
    Assert.That(_stateMachine.CanTransition(PrintJobStatus.Unknown, PrintJobStatus.HandledOnPaper),
                Is.True);
  }

  [Test]
  public void CanTransition_BlockedToHandledOnPaper_AcknowledgedAtAStationThatCannotPrint_IsAllowed()
  {
    Assert.That(_stateMachine.CanTransition(PrintJobStatus.Blocked, PrintJobStatus.HandledOnPaper),
                Is.True);
  }

  [Test]
  public void CanTransition_QueuedToHandledOnPaper_AcknowledgedAtAStationThatCannotPrint_IsAllowed()
  {
    Assert.That(_stateMachine.CanTransition(PrintJobStatus.Queued, PrintJobStatus.HandledOnPaper),
                Is.True);
  }


  [Test]
  public void CanTransition_PrintingToBlocked_PreflightFoundPaperEndOrCoverOpenAfterTheClaim_IsAllowed()
  {
    Assert.That(_stateMachine.CanTransition(PrintJobStatus.Sending, PrintJobStatus.Blocked),
                Is.True);
  }

  [Test]
  public void CanTransition_EveryPairThatIsNotDrawn_IsRefused()
  {
    HashSet<(PrintJobStatus From, PrintJobStatus To)> drawnEdges = DrawnEdges();
    PrintJobStatus[] allStatuses = Enum.GetValues<PrintJobStatus>();
    var refusedPairs = 0;

    foreach (var from in allStatuses)
    {
      foreach (var to in allStatuses)
      {
        if (drawnEdges.Contains((from, to)))
        {
          continue;
        }

        Assert.That(_stateMachine.CanTransition(from, to),
                    Is.False,
                    $"{from} to {to} is not drawn in the diagram");
        refusedPairs++;
      }
    }

    var everyPair = Enum.GetValues<PrintJobStatus>().Length * Enum.GetValues<PrintJobStatus>().Length;

    Assert.That(refusedPairs, Is.EqualTo(everyPair - drawnEdges.Count));
  }

  [Test]
  public void CanTransition_EveryDrawnEdge_IsAllowed()
  {
    foreach (var edge in DrawnEdges())
    {
      Assert.That(_stateMachine.CanTransition(edge.From, edge.To),
                  Is.True,
                  $"{edge.From} to {edge.To}");
    }
  }

  [Test]
  public void CanTransition_OutOfATerminalState_IsRefused()
  {
    Assert.Multiple(() =>
                    {
                      Assert.That(_stateMachine.CanTransition(PrintJobStatus.Printed, PrintJobStatus.Queued), Is.False);
                      Assert.That(_stateMachine.CanTransition(PrintJobStatus.Printed, PrintJobStatus.Sending), Is.False);
                      Assert.That(_stateMachine.CanTransition(PrintJobStatus.HandledOnPaper, PrintJobStatus.Queued), Is.False);
                    });
  }
}
