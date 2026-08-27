using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Services;

public sealed class OrderStatusCalculator
{
    public OrderStatus Calculate(IReadOnlyCollection<LocationTicketStatus> ticketStatuses)
    {
        if (ticketStatuses.Any(NeedsHumanAttention))
        {
            return OrderStatus.NeedsAttention;
        }

        if (ticketStatuses.All(IsOnPaper))
        {
            return OrderStatus.Printed;
        }

        if (ticketStatuses.Any(status => status == LocationTicketStatus.Printing))
        {
            return OrderStatus.Printing;
        }

        return OrderStatus.Accepted;
    }

    private bool NeedsHumanAttention(LocationTicketStatus status)
    {
        return status switch
        {
            LocationTicketStatus.Unknown => true,
            LocationTicketStatus.Failed => true,
            LocationTicketStatus.Blocked => true,
            LocationTicketStatus.Queued => false,
            LocationTicketStatus.Printing => false,
            LocationTicketStatus.Printed => false,
            LocationTicketStatus.PrintedOnTestPrinter => false,
            LocationTicketStatus.HandledOnPaper => false,
            _ => new Never().OfType<bool>(status),
        };
    }

    private bool IsOnPaper(LocationTicketStatus status)
    {
        return status switch
        {
            LocationTicketStatus.Printed => true,
            LocationTicketStatus.HandledOnPaper => true,
            LocationTicketStatus.PrintedOnTestPrinter => true,
            LocationTicketStatus.Queued => false,
            LocationTicketStatus.Blocked => false,
            LocationTicketStatus.Printing => false,
            LocationTicketStatus.Unknown => false,
            LocationTicketStatus.Failed => false,
            _ => new Never().OfType<bool>(status),
        };
    }
}
