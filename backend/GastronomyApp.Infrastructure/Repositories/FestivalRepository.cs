using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.QueryRows;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class FestivalRepository : IFestivalRepository
{
  private const int FirstNumber = 1;

  private readonly GastronomyAppDbContext _dbContext;
  private readonly TypeAdapterConfig _mapperConfig;
  private readonly FestivalSchedule _schedule;

  public FestivalRepository(GastronomyAppDbContext dbContext, FestivalSchedule schedule, TypeAdapterConfig mapperConfig)
  {
    _dbContext = dbContext;
    _schedule = schedule;
    _mapperConfig = mapperConfig;
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

  public async Task<IReadOnlyList<FestivalContentCounts>> FindContentCountsAsync(CancellationToken cancellationToken)
  {
    return await _dbContext.Festivals.AsNoTracking()
                           .Select(festival => new FestivalContentCountsRow
                                               {
                                                 Festival = festival,
                                                 StationCount = _dbContext.FestivalStations.Count(link => link.FestivalId == festival.Id),
                                                 MenuItemCount = _dbContext.FestivalCatalogItems.Count(menuRow => menuRow.FestivalId == festival.Id),
                                                 OrderCount = _dbContext.Orders.Count(order => order.FestivalId == festival.Id)
                                               })
                           .ProjectToType<FestivalContentCounts>(_mapperConfig)
                           .ToListAsync(cancellationToken);
  }

  public async Task AddAsync(Festival festival, CancellationToken cancellationToken)
  {
    await _dbContext.Festivals.AddAsync(festival, cancellationToken);
  }

  public async Task CopyContentsAsync(Guid copiedFromFestivalId, Guid newFestivalId, CancellationToken cancellationToken)
  {
    List<FestivalStation> stationLinks = await _dbContext.FestivalStations.AsNoTracking().Where(link => link.FestivalId == copiedFromFestivalId).ToListAsync(cancellationToken);

    foreach (var link in stationLinks)
      _dbContext.FestivalStations.Add(new()
                                      {
                                        Id = Guid.NewGuid(),
                                        FestivalId = newFestivalId,
                                        StationId = link.StationId,
                                        NextStationOrderNumber = FirstNumber
                                      });

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
