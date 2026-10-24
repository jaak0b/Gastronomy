using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class FestivalRepository : IFestivalRepository
{
  private const int FirstNumber = 1;

  private readonly AreaShapeService _areaShapeService;
  private readonly GastronomyAppDbContext _dbContext;
  private readonly FestivalSchedule _schedule;

  public FestivalRepository(GastronomyAppDbContext dbContext, FestivalSchedule schedule, AreaShapeService areaShapeService)
  {
    _dbContext = dbContext;
    _schedule = schedule;
    _areaShapeService = areaShapeService;
  }

  public async Task<Festival?> FindRunningAsync(DateTime nowUtc, CancellationToken cancellationToken)
  {
    List<Festival> shown = await _dbContext.Festivals.AsNoTracking().Where(festival => !festival.IsHidden).ToListAsync(cancellationToken);

    return _schedule.FindRunningAt(shown, nowUtc);
  }

  public async Task<IReadOnlyCollection<Festival>> FindAllAsync(CancellationToken cancellationToken)
  {
    return await _dbContext.Festivals.AsNoTracking().OrderByDescending(festival => festival.StartsAtUtc).ToListAsync(cancellationToken);
  }

  public async Task<Festival?> FindByIdAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.Festivals.FirstOrDefaultAsync(festival => festival.Id == festivalId, cancellationToken);
  }

  public async Task<bool> ExistsAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.Festivals.AsNoTracking().AnyAsync(festival => festival.Id == festivalId, cancellationToken);
  }

  public async Task<IReadOnlyList<Guid>> FindIdsNotEndedAsync(DateTime nowUtc, CancellationToken cancellationToken)
  {
    return await _dbContext.Festivals.AsNoTracking().Where(festival => !festival.IsHidden && festival.EndsAtUtc > nowUtc).Select(festival => festival.Id).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<Festival>> FindAllWithContentsAsync(CancellationToken cancellationToken)
  {
    return await _dbContext.Festivals.AsNoTracking().OrderByDescending(festival => festival.StartsAtUtc).Include(festival => festival.Stations).Include(festival => festival.CatalogItems).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyDictionary<Guid, int>> CountOrdersByFestivalAsync(CancellationToken cancellationToken)
  {
    return await _dbContext.Orders.AsNoTracking().GroupBy(order => order.FestivalId).ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);
  }

  public async Task AddAsync(Festival festival, CancellationToken cancellationToken)
  {
    await _dbContext.Festivals.AddAsync(festival, cancellationToken);
  }

  public async Task CopyContentsAsync(Guid copiedFromFestivalId, Guid newFestivalId, CancellationToken cancellationToken)
  {
    List<FestivalStation> stationLinks = await _dbContext.FestivalStations.AsNoTracking().Where(link => link.FestivalId == copiedFromFestivalId).ToListAsync(cancellationToken);
    Dictionary<Guid, Guid> linkIdsAtTheCopy = stationLinks.ToDictionary(link => link.Id, link => Guid.NewGuid());

    foreach (var link in stationLinks)
      _dbContext.FestivalStations.Add(new()
                                      {
                                        Id = linkIdsAtTheCopy[link.Id],
                                        FestivalId = newFestivalId,
                                        StationId = link.StationId,
                                        NextStationOrderNumber = FirstNumber
                                      });

    List<Area> shownAreas = await _dbContext.Areas.AsNoTracking().Where(area => area.FestivalId == copiedFromFestivalId && !area.IsHidden).Include(area => area.Tables.Where(table => !table.IsHidden)).Include(area => area.StationShapes).ToListAsync(cancellationToken);

    foreach (var area in shownAreas)
    {
      var areaIdAtTheCopy = Guid.NewGuid();

      _dbContext.Areas.Add(new()
                           {
                             Id = areaIdAtTheCopy,
                             FestivalId = newFestivalId,
                             Name = area.Name,
                             SortOrder = area.SortOrder,
                             IsHidden = false
                           });

      foreach (var table in area.Tables)
      {
        Table copiedTable = new()
                            {
                              Id = Guid.NewGuid(),
                              FestivalId = newFestivalId,
                              AreaId = areaIdAtTheCopy,
                              Name = table.Name,
                              IsHidden = false
                            };

        _areaShapeService.Reshape(copiedTable, _areaShapeService.GeometryOf(table));
        _dbContext.Tables.Add(copiedTable);
      }

      foreach (var shape in area.StationShapes)
      {
        FestivalStationShape copiedShape = new()
                                           {
                                             FestivalStationId = linkIdsAtTheCopy[shape.FestivalStationId],
                                             FestivalId = newFestivalId,
                                             AreaId = areaIdAtTheCopy
                                           };

        _areaShapeService.Reshape(copiedShape, _areaShapeService.GeometryOf(shape));
        _dbContext.FestivalStationShapes.Add(copiedShape);
      }
    }

    List<FestivalCatalogItem> menuRows = await _dbContext.FestivalCatalogItems.AsNoTracking().Where(menuRow => menuRow.FestivalId == copiedFromFestivalId).ToListAsync(cancellationToken);

    foreach (var menuRow in menuRows)
      _dbContext.FestivalCatalogItems.Add(new()
                                          {
                                            Id = Guid.NewGuid(),
                                            FestivalId = newFestivalId,
                                            CatalogItemId = menuRow.CatalogItemId,
                                            PriceCents = menuRow.PriceCents,
                                            IsAvailable = true
                                          });

    List<ItemStationAssignment> assignments = await _dbContext.ItemStationAssignments.AsNoTracking().Where(assignment => assignment.FestivalId == copiedFromFestivalId).ToListAsync(cancellationToken);

    foreach (var assignment in assignments)
      _dbContext.ItemStationAssignments.Add(new()
                                            {
                                              Id = Guid.NewGuid(),
                                              FestivalId = newFestivalId,
                                              CatalogItemId = assignment.CatalogItemId,
                                              StationId = assignment.StationId
                                            });
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
