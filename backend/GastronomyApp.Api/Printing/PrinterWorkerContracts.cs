using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Api.Printing;

public interface IPrintCallbacks
{
    public Task OnPrintJobStatusChangedAsync(Guid orderId, Guid stationOrderId, PrintJobStatus newStatus, PrintFailureReason? failureReason, CancellationToken ct);

    public Task OnOrderStatusChangedAsync(Guid orderId, OrderStatus newStatus, CancellationToken ct);

    public Task OnPrinterStatusChangedAsync(Guid printerId, IReadOnlyList<Guid> stationIds, PrinterStatusSnapshot snapshot, bool isFaulty, int waitingPrintJobCount, CancellationToken ct);
}

public sealed record PrinterWorkerDomainServices(
    RetryPolicy RetryPolicy,
    GiveUpWindowCalculator GiveUpWindowCalculator,
    OrderStatusCalculator OrderStatusCalculator,
    PrintJobStateMachine PrintJobStateMachine);
