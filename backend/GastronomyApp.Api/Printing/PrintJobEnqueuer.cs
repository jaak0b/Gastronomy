using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Printing;

public sealed class PrintJobEnqueuer
{
    private readonly IPrinterFleet printerFleet;
    private readonly ILogger<PrintJobEnqueuer> logger;

    public PrintJobEnqueuer(IPrinterFleet printerFleet, ILogger<PrintJobEnqueuer> logger)
    {
        this.printerFleet = printerFleet;
        this.logger = logger;
    }

    public async Task EnqueueWithoutFailingTheCallerAsync(
        Guid stationOrderId,
        CancellationToken cancellationToken)
    {
        try
        {
            await printerFleet.EnqueueAsync(stationOrderId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "The station order {StationOrderId} could not be handed to a printer worker, so it stays waiting at its station until it is handed over again.",
                stationOrderId);
        }
    }
}
