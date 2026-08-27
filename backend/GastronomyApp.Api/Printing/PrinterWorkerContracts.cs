using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Api.Printing;

public interface IPrintCallbacks
{
    public Task OnTicketStatusChangedAsync(Guid orderId, Guid locationTicketId, LocationTicketStatus newStatus, PrintFailureReason? failureReason, CancellationToken ct);

    public Task OnOrderStatusChangedAsync(Guid orderId, OrderStatus newStatus, CancellationToken ct);

    public Task OnPrinterStatusChangedAsync(Guid stationId, PrinterStatusSnapshot snapshot, bool isFaulty, int waitingTicketCount, CancellationToken ct);
}

public sealed record PrinterWorkerDomainServices(
    RetryPolicy RetryPolicy,
    GiveUpWindowCalculator GiveUpWindowCalculator,
    OrderStatusCalculator OrderStatusCalculator,
    TicketStateMachine TicketStateMachine,
    PrintJobStateMachine PrintJobStateMachine,
    PrinterEndpointKeyBuilder EndpointKeyBuilder);
