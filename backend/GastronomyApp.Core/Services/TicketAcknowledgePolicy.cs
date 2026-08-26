using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class TicketAcknowledgePolicy
{
    public bool CanAcknowledge(LocationTicketStatus ticketStatus, StationPrintability station)
    {
        if (ticketStatus is LocationTicketStatus.Failed
            or LocationTicketStatus.Unknown
            or LocationTicketStatus.Blocked)
        {
            return true;
        }

        bool stationCannotPrintRightNow = station.IsFaulty
            || !station.IsOnline
            || station.IsPaperEnd
            || station.IsCoverOpen
            || station.IsInErrorState
            || !station.IsEnabled;

        return stationCannotPrintRightNow && ticketStatus is not LocationTicketStatus.Printing;
    }
}
