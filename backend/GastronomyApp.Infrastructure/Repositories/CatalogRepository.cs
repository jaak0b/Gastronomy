using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class CatalogRepository : ICatalogRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public CatalogRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<CatalogAtFestival> ReadAtFestivalAsync(Guid festivalId,
                                                           string festivalName,
                                                           CancellationToken cancellationToken)
  {
    List<CatalogStationRow> stations = await _dbContext.Stations
                                                       .AsNoTracking()
                                                       .Join(_dbContext.FestivalStations.AsNoTracking(),
                                                             station => station.Id,
                                                             link => link.StationId,
                                                             (station, link) => new { Station = station, Link = link })
                                                       .Where(joined => joined.Link.FestivalId == festivalId
                                                                        && joined.Station.IsActive)
                                                       .OrderBy(joined => joined.Station.SortOrder)
                                                       .Select(joined => new CatalogStationRow(joined.Station.Id,
                                                                                               joined.Station.Name,
                                                                                               joined.Station.SortOrder))
                                                       .ToListAsync(cancellationToken);

    HashSet<Guid> stationIdsAtTheFestival = [.. stations.Select(station => station.StationId)];

    List<CatalogCategoryRow> categories = await _dbContext.CatalogCategories
                                                          .AsNoTracking()
                                                          .Where(category => category.IsActive)
                                                          .OrderBy(category => category.SortOrder)
                                                          .Select(category => new CatalogCategoryRow(category.Id,
                                                                                                     category.Name,
                                                                                                     category.ColourHex,
                                                                                                     category.SortOrder))
                                                          .ToListAsync(cancellationToken);

    HashSet<Guid> activeCategoryIds = [.. categories.Select(category => category.CategoryId)];

    List<MenuItemRow> menuRows = await _dbContext.FestivalCatalogItems
                                                 .AsNoTracking()
                                                 .Join(_dbContext.CatalogItems.AsNoTracking(),
                                                       menuRow => menuRow.CatalogItemId,
                                                       item => item.Id,
                                                       (menuRow, item) => new { MenuRow = menuRow, Item = item })
                                                 .Where(joined => joined.MenuRow.FestivalId == festivalId
                                                                  && joined.Item.IsActive)
                                                 .OrderBy(joined => joined.Item.SortOrder)
                                                 .Select(joined => new MenuItemRow(joined.Item.Id,
                                                                                   joined.Item.CategoryId,
                                                                                   joined.Item.Name,
                                                                                   joined.MenuRow.PriceCents,
                                                                                   joined.Item.SortOrder,
                                                                                   joined.MenuRow.IsAvailable,
                                                                                   joined.Item.ProductionMinutes,
                                                                                   joined.Item.IsQueueIndependent))
                                                 .ToListAsync(cancellationToken);

    List<AssignmentRow> assignments = await _dbContext.ItemStationAssignments
                                                      .AsNoTracking()
                                                      .Where(assignment => assignment.FestivalId == festivalId)
                                                      .Select(assignment => new AssignmentRow(assignment.CatalogItemId,
                                                                                              assignment.StationId))
                                                      .ToListAsync(cancellationToken);

    IReadOnlyList<CatalogItemRow> items =
    [
      .. menuRows.Where(row => activeCategoryIds.Contains(row.CategoryId))
                 .Select(row => new CatalogItemRow(row.ItemId,
                                                   row.CategoryId,
                                                   row.Name,
                                                   row.PriceCents,
                                                   row.SortOrder,
                                                   row.IsAvailable,
                                                   row.ProductionMinutes,
                                                   row.IsQueueIndependent,
                                                   [
                                                     .. assignments
                                                       .Where(assignment => assignment.CatalogItemId == row.ItemId
                                                                            && stationIdsAtTheFestival.Contains(assignment.StationId))
                                                       .Select(assignment => assignment.StationId)
                                                   ]))
    ];

    return new(festivalId, festivalName, stations, categories, items);
  }

  private sealed record MenuItemRow(
    Guid ItemId,
    Guid CategoryId,
    string Name,
    int PriceCents,
    int SortOrder,
    bool IsAvailable,
    double? ProductionMinutes,
    bool IsQueueIndependent);

  private sealed record AssignmentRow(Guid CatalogItemId, Guid StationId);
}
