namespace GastronomyApp.Core.Enums;

public enum PrintJobStatus
{
    Queued,
    PreflightCheck,
    Blocked,
    Sending,
    AwaitingEcho,
    Confirmed,
    Unknown,
    Failed,
    ResolvedPrinted,
    ResolvedMissing,
}
