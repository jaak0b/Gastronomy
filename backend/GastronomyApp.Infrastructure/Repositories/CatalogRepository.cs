using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.QueryRows;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class CatalogRepository : ICatalogRepository
{
  private readonly GastronomyAppDbContext _dbContext;
  private readonly TypeAdapterConfig _mapperConfig;

  public CatalogRepository(GastronomyAppDbContext dbContext, TypeAdapterConfig mapperConfig)
  {
    _dbContext = dbContext;
    _mapperConfig = mapperConfig;
  }

  public async Task<CatalogAtFestival> ReadAtFestivalAsync(Guid festivalId, string festivalName, CancellationToken cancellationToken)
  {
    List<CatalogStationRow> stations = await _dbContext.Stations.AsNoTracking()
                                                       .Join(_dbContext.FestivalStations.AsNoTracking(),
                                                             station => station.Id,
                                                             link => link.StationId,
                                                             (station, link) => new
                                                                                {
                                                                                  Station = station,
                                                                                  Link = link
                                                                                })
                                                       .Where(joined => joined.Link.FestivalId == festivalId && joined.Station.IsActive)
                                                       .OrderBy(joined => joined.Station.SortOrder)
                                                       .Select(joined => joined.Station)
                                                       .ProjectToType<CatalogStationRow>(_mapperConfig)
                                                       .ToListAsync(cancellationToken);

    List<Guid> stationIdsAtTheFestival = stations.Select(station => station.StationId).ToList();

    List<CatalogCategoryRow> categories = await _dbContext.CatalogCategories.AsNoTracking().Where(category => category.IsActive).OrderBy(category => category.SortOrder).ProjectToType<CatalogCategoryRow>(_mapperConfig).ToListAsync(cancellationToken);

    List<Guid> activeCategoryIds = categories.Select(category => category.CategoryId).ToList();

    List<FestivalMenuItemRow> menuRows = await _dbContext.FestivalCatalogItems.AsNoTracking()
                                                         .Join(_dbContext.CatalogItems.AsNoTracking(),
                                                               menuRow => menuRow.CatalogItemId,
                                                               item => item.Id,
                                                               (menuRow, item) => new
                                                                                  {
                                                                                    MenuRow = menuRow,
                                                                                    Item = item
                                                                                  })
                                                         .Where(joined => joined.MenuRow.FestivalId == festivalId && joined.Item.IsActive && activeCategoryIds.Contains(joined.Item.CategoryId))
                                                         .OrderBy(joined => joined.Item.SortOrder)
                                                         .Select(joined => new FestivalMenuItemRow
                                                                           {
                                                                             MenuRow = joined.MenuRow,
                                                                             Item = joined.Item,
                                                                             StationIds = _dbContext.ItemStationAssignments.AsNoTracking()
                                                                                                    .Where(assignment => assignment.FestivalId == festivalId && assignment.CatalogItemId == joined.Item.Id && stationIdsAtTheFestival.Contains(assignment.StationId))
                                                                                                    .Select(assignment => assignment.StationId)
                                                                                                    .ToList()
                                                                           })
                                                         .ToListAsync(cancellationToken);

    IReadOnlyList<CatalogItemRow> items = menuRows.Select(row => row.Adapt<CatalogItemRow>(_mapperConfig)).ToList();

    return new(festivalId, festivalName, stations, categories, items);
  }
}
