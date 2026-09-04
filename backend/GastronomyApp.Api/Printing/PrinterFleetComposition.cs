using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Printing;

public sealed class DatabasePrinterSource : IPrinterSource
{
  private readonly IDbContextFactory<GastronomyAppDbContext> _contextFactory;

  public DatabasePrinterSource(IDbContextFactory<GastronomyAppDbContext> contextFactory)
  {
    _contextFactory = contextFactory;
  }

  public async Task<IReadOnlyList<PrinterWithStations>> LoadActiveAsync(CancellationToken ct)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(ct);

    List<Station> stations = await context.Stations
                                          .Where(station => station.IsActive && station.PrinterId != null)
                                          .OrderBy(station => station.SortOrder)
                                          .ToListAsync(ct);

    List<Printer> printers = await context.Printers.ToListAsync(ct);

    List<PrinterWithStations> entries = [];

    foreach (var printer in printers)
    {
      Guid[] served =
      [
        .. stations
          .Where(station => station.PrinterId == printer.Id)
          .Select(station => station.Id)
      ];

      entries.Add(new(printer, served));
    }

    return entries;
  }
}
