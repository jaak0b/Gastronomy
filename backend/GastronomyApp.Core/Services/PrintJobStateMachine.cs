using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Services;

public sealed class PrintJobStateMachine
{
    public bool CanTransition(PrintJobStatus from, PrintJobStatus to)
    {
        return (from, to) switch
        {
            (PrintJobStatus.Queued, PrintJobStatus.PreflightCheck) => true,
            (PrintJobStatus.Queued, PrintJobStatus.Failed) => true,
            (PrintJobStatus.PreflightCheck, PrintJobStatus.Blocked) => true,
            (PrintJobStatus.PreflightCheck, PrintJobStatus.Queued) => true,
            (PrintJobStatus.PreflightCheck, PrintJobStatus.Sending) => true,
            (PrintJobStatus.Blocked, PrintJobStatus.Queued) => true,
            (PrintJobStatus.Blocked, PrintJobStatus.Failed) => true,
            (PrintJobStatus.Sending, PrintJobStatus.AwaitingEcho) => true,
            (PrintJobStatus.Sending, PrintJobStatus.Queued) => true,
            (PrintJobStatus.Sending, PrintJobStatus.Unknown) => true,
            (PrintJobStatus.AwaitingEcho, PrintJobStatus.Confirmed) => true,
            (PrintJobStatus.AwaitingEcho, PrintJobStatus.Unknown) => true,
            (PrintJobStatus.Unknown, PrintJobStatus.ResolvedPrinted) => true,
            (PrintJobStatus.Unknown, PrintJobStatus.ResolvedMissing) => true,
            _ => false,
        };
    }
}
