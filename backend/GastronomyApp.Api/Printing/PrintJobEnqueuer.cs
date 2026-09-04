using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Printing;

public sealed class PrintJobEnqueuer
{
  private readonly ILogger<PrintJobEnqueuer> _logger;
  private readonly IPrinterFleet _printerFleet;

  public PrintJobEnqueuer(IPrinterFleet printerFleet, ILogger<PrintJobEnqueuer> logger)
  {
    _printerFleet = printerFleet;
    _logger = logger;
  }

  public async Task EnqueueWithoutFailingTheCallerAsync(Guid stationOrderId,
                                                        CancellationToken cancellationToken)
  {
    try
    {
      await _printerFleet.EnqueueAsync(stationOrderId, cancellationToken);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
      _logger.LogError(exception,
                      "The station order {StationOrderId} could not be handed to a printer worker, so it stays waiting at its station until it is handed over again.",
                      stationOrderId);
    }
  }
}
