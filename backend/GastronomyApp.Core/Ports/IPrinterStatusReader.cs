using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Ports;

public interface IPrinterStatusReader
{
  public Task<StationPrintability?> GetCurrentAsync(Guid stationId, CancellationToken cancellationToken);
}
