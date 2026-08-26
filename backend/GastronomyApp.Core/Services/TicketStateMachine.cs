using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Services;

public sealed class TicketStateMachine
{
    public bool CanTransition(LocationTicketStatus from, LocationTicketStatus to)
    {
        return (from, to) switch
        {
            (LocationTicketStatus.Queued, LocationTicketStatus.Printing) => true,
            (LocationTicketStatus.Queued, LocationTicketStatus.Blocked) => true,
            (LocationTicketStatus.Queued, LocationTicketStatus.Failed) => true,
            (LocationTicketStatus.Queued, LocationTicketStatus.HandledOnPaper) => true,
            (LocationTicketStatus.Printing, LocationTicketStatus.Printed) => true,
            (LocationTicketStatus.Printing, LocationTicketStatus.PrintedOnTestPrinter) => true,
            (LocationTicketStatus.Printing, LocationTicketStatus.Unknown) => true,
            (LocationTicketStatus.Printing, LocationTicketStatus.Queued) => true,
            (LocationTicketStatus.Blocked, LocationTicketStatus.Queued) => true,
            (LocationTicketStatus.Blocked, LocationTicketStatus.Failed) => true,
            (LocationTicketStatus.Blocked, LocationTicketStatus.HandledOnPaper) => true,
            (LocationTicketStatus.Unknown, LocationTicketStatus.Printed) => true,
            (LocationTicketStatus.Unknown, LocationTicketStatus.Queued) => true,
            (LocationTicketStatus.Unknown, LocationTicketStatus.HandledOnPaper) => true,
            (LocationTicketStatus.Failed, LocationTicketStatus.Queued) => true,
            (LocationTicketStatus.Failed, LocationTicketStatus.HandledOnPaper) => true,
            (LocationTicketStatus.Printed, LocationTicketStatus.Queued) => true,
            (LocationTicketStatus.PrintedOnTestPrinter, LocationTicketStatus.Queued) => true,
            _ => false,
        };
    }
}
