using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class ItemOrderabilityRepository : IItemOrderabilityRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public ItemOrderabilityRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<IReadOnlyList<Guid>> FindActiveStationIdsAtFestivalAsync(Guid festivalId,
                                                                             CancellationToken cancellationToken)
  {
    return await _dbContext.FestivalStations
                           .AsNoTracking()
                           .Join(_dbContext.Stations.AsNoTracking(),
                                 link => link.StationId,
                                 station => station.Id,
                                 (link, station) => new { Link = link, Station = station })
                           .Where(joined => joined.Link.FestivalId == festivalId && joined.Station.IsActive)
                           .Select(joined => joined.Station.Id)
                           .ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<Guid>> FindItemIdsPreparedByAsync(Guid festivalId,
                                                                    IReadOnlyCollection<Guid> stationIds,
                                                                    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(stationIds);

    List<Guid> preparing = [.. stationIds];

    return await _dbContext.ItemStationAssignments
                           .AsNoTracking()
                           .Where(assignment => assignment.FestivalId == festivalId
                                                && preparing.Contains(assignment.StationId))
                           .Select(assignment => assignment.CatalogItemId)
                           .Distinct()
                           .ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<Guid>> FindActiveMenuItemIdsAsync(Guid festivalId,
                                                                    CancellationToken cancellationToken)
  {
    return await _dbContext.FestivalCatalogItems
                           .AsNoTracking()
                           .Join(_dbContext.CatalogItems.AsNoTracking(),
                                 menuRow => menuRow.CatalogItemId,
                                 item => item.Id,
                                 (menuRow, item) => new { MenuRow = menuRow, Item = item })
                           .Where(joined => joined.MenuRow.FestivalId == festivalId && joined.Item.IsActive)
                           .OrderBy(joined => joined.Item.SortOrder)
                           .Select(joined => joined.Item.Id)
                           .ToListAsync(cancellationToken);
  }
}
