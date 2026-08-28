using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Services;

public sealed class PrintJobStateMachine
{
  public bool CanTransition(PrintJobStatus from, PrintJobStatus to)
  {
    return (from, to) switch
    {
      (PrintJobStatus.Queued, PrintJobStatus.Sending) => true,
      (PrintJobStatus.Queued, PrintJobStatus.Blocked) => true,
      (PrintJobStatus.Queued, PrintJobStatus.Failed) => true,
      (PrintJobStatus.Queued, PrintJobStatus.HandledOnPaper) => true,
      (PrintJobStatus.Sending, PrintJobStatus.Printed) => true,
      (PrintJobStatus.Sending, PrintJobStatus.Unknown) => true,
      (PrintJobStatus.Sending, PrintJobStatus.Queued) => true,
      (PrintJobStatus.Sending, PrintJobStatus.Blocked) => true,
      (PrintJobStatus.Blocked, PrintJobStatus.Queued) => true,
      (PrintJobStatus.Blocked, PrintJobStatus.Failed) => true,
      (PrintJobStatus.Blocked, PrintJobStatus.HandledOnPaper) => true,
      (PrintJobStatus.Unknown, PrintJobStatus.Printed) => true,
      (PrintJobStatus.Unknown, PrintJobStatus.Queued) => true,
      (PrintJobStatus.Unknown, PrintJobStatus.HandledOnPaper) => true,
      (PrintJobStatus.Failed, PrintJobStatus.HandledOnPaper) => true,
      _ => false,
    };
  }
}
