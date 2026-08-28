using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public sealed class StationPrinterStatusLookup
{
  public async Task<Dictionary<Guid, PrinterStatus>> ByStationAsync(GastronomyAppDbContext dbContext,
                                                                    IReadOnlyCollection<Guid> stationIds,
                                                                    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);
    ArgumentNullException.ThrowIfNull(stationIds);

    List<Guid> wanted = [.. stationIds];

    List<Station> stations = await dbContext.Stations
                                            .AsNoTracking()
                                            .Where(station => wanted.Contains(station.Id) && station.PrinterId != null)
                                            .ToListAsync(cancellationToken);

    List<Guid> printerIds = [.. stations.Select(station => station.PrinterId!.Value).Distinct()];

    Dictionary<Guid, PrinterStatus> byPrinterId = await dbContext.PrinterStatuses
                                                                 .AsNoTracking()
                                                                 .Where(status => printerIds.Contains(status.PrinterId))
                                                                 .ToDictionaryAsync(status => status.PrinterId, cancellationToken);

    Dictionary<Guid, PrinterStatus> byStationId = [];
    foreach (var station in stations)
    {
      if (byPrinterId.TryGetValue(station.PrinterId!.Value, out var status))
      {
        byStationId[station.Id] = status;
      }
    }

    return byStationId;
  }
}
