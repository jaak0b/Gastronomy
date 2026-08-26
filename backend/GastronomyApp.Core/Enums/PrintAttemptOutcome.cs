namespace GastronomyApp.Core.Enums;

public enum PrintAttemptOutcome
{
    Confirmed,
    Blocked,
    Unreachable,
    SocketDropped,
    Timeout,
    PrinterError,
}
