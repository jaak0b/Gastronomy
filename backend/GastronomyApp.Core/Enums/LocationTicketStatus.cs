namespace GastronomyApp.Core.Enums;

public enum LocationTicketStatus
{
    Queued,
    Blocked,
    Printing,
    Printed,
    PrintedOnTestPrinter,
    Unknown,
    Failed,
    HandledOnPaper,
}
