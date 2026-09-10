using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public sealed class ItemsLeftWithoutAStation
{
  private readonly IClock _clock;
  private readonly GastronomyAppDbContext _dbContext;

  public ItemsLeftWithoutAStation(GastronomyAppDbContext dbContext, IClock clock)
  {
    _dbContext = dbContext;
    _clock = clock;
  }

  public async Task<bool> WouldHappenToAnItemAssignedToAsync(IReadOnlyCollection<Guid> stationIds,
                                                             CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(stationIds);

    HashSet<Guid> activeStationIds = await ActiveStationIdsAsync(cancellationToken);

    return !stationIds.Any(activeStationIds.Contains);
  }

  public async Task<IReadOnlyList<Guid>> WhenTheStationIsSwitchedOffAsync(Guid stationId,
                                                                         CancellationToken cancellationToken)
  {
    var nowUtc = _clock.UtcNow;

    List<Guid> festivalIdsStillToCome = await _dbContext.Festivals
                                                        .AsNoTracking()
                                                        .Where(festival => !festival.IsHidden
                                                                           && festival.EndsAtUtc > nowUtc)
                                                        .Select(festival => festival.Id)
                                                        .ToListAsync(cancellationToken);

    List<ItemStationAssignment> assignments = await _dbContext.ItemStationAssignments
                                                              .AsNoTracking()
                                                              .Where(assignment =>
                                                                       festivalIdsStillToCome.Contains(assignment.FestivalId))
                                                              .ToListAsync(cancellationToken);

    List<Guid> itemIdsHere =
    [
      .. assignments.Where(assignment => assignment.StationId == stationId)
                    .Select(assignment => assignment.CatalogItemId)
                    .Distinct()
    ];

    List<Guid> activeItemIds = await _dbContext.CatalogItems
                                               .AsNoTracking()
                                               .Where(item => item.IsActive && itemIdsHere.Contains(item.Id))
                                               .Select(item => item.Id)
                                               .ToListAsync(cancellationToken);

    return await LosingTheirLastActiveStationAsync(assignments,
                                                   stationId,
                                                   activeItemIds.Contains,
                                                   cancellationToken);
  }

  public async Task<IReadOnlyList<Guid>> WhenTheStationLeavesTheFestivalAsync(Guid festivalId,
                                                                             Guid stationId,
                                                                             CancellationToken cancellationToken)
  {
    List<ItemStationAssignment> assignments = await _dbContext.ItemStationAssignments
                                                              .AsNoTracking()
                                                              .Where(assignment => assignment.FestivalId == festivalId)
                                                              .ToListAsync(cancellationToken);

    List<Guid> itemIdsOnTheMenu = await _dbContext.FestivalCatalogItems
                                                  .AsNoTracking()
                                                  .Where(menuRow => menuRow.FestivalId == festivalId)
                                                  .Select(menuRow => menuRow.CatalogItemId)
                                                  .ToListAsync(cancellationToken);

    return await LosingTheirLastActiveStationAsync(assignments,
                                                   stationId,
                                                   itemIdsOnTheMenu.Contains,
                                                   cancellationToken);
  }

  private async Task<IReadOnlyList<Guid>> LosingTheirLastActiveStationAsync(
    List<ItemStationAssignment> assignments,
    Guid stationId,
    Func<Guid, bool> itemStillCounts,
    CancellationToken cancellationToken)
  {
    HashSet<Guid> activeStationIds = await ActiveStationIdsAsync(cancellationToken);

    return
    [
      .. assignments.Where(assignment => assignment.StationId == stationId
                                         && itemStillCounts(assignment.CatalogItemId)
                                         && !assignments.Any(other => other.FestivalId == assignment.FestivalId
                                                                      && other.CatalogItemId == assignment.CatalogItemId
                                                                      && other.StationId != stationId
                                                                      && activeStationIds.Contains(other.StationId)))
                    .Select(assignment => assignment.CatalogItemId)
                    .Distinct()
    ];
  }

  private async Task<HashSet<Guid>> ActiveStationIdsAsync(CancellationToken cancellationToken)
  {
    return
    [
      .. await _dbContext.Stations
                         .AsNoTracking()
                         .Where(station => station.IsActive)
                         .Select(station => station.Id)
                         .ToListAsync(cancellationToken)
    ];
  }
}
