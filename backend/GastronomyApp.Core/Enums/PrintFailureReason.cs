namespace GastronomyApp.Core.Enums;

public enum PrintFailureReason
{
    PaperEnd,
    CoverOpen,
    Unreachable,
    Timeout,
    SocketDropped,
    PrinterError,
    StationDisabled,
    StationFaulty,
    TicketResolvedByHuman,
}
