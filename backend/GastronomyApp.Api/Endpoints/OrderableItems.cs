using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public sealed class OrderableItems
{
  private readonly IClock _clock;

  public OrderableItems(IClock clock)
  {
    _clock = clock;
  }

  public Task<IReadOnlyList<Guid>> IdsAtAsync(GastronomyAppDbContext dbContext,
                                              Guid festivalId,
                                              CancellationToken cancellationToken)
  {
    return IdsAtAsync(dbContext, festivalId, [], cancellationToken);
  }

  public async Task<bool> AnyOfThemWouldPrepareAtAsync(GastronomyAppDbContext dbContext,
                                                       Guid festivalId,
                                                       IReadOnlyCollection<Guid> stationIds,
                                                       CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(stationIds);

    List<Guid> stationsPreparing = await StationsPreparingAtAsync(dbContext, festivalId, [], cancellationToken);

    return stationsPreparing.Any(stationIds.Contains);
  }

  public async Task<IReadOnlyList<Guid>> WouldStopBeingOrderableAtAsync(GastronomyAppDbContext dbContext,
                                                                        Guid festivalId,
                                                                        IReadOnlyCollection<Guid> stationsNoLongerPreparing,
                                                                        CancellationToken cancellationToken)
  {
    IReadOnlyList<Guid> asItIsNow = await IdsAtAsync(dbContext, festivalId, [], cancellationToken);
    IReadOnlyList<Guid> afterTheChange = await IdsAtAsync(dbContext,
                                                          festivalId,
                                                          stationsNoLongerPreparing,
                                                          cancellationToken);

    return [.. asItIsNow.Except(afterTheChange)];
  }

  public async Task<IReadOnlyList<Guid>> WouldStopBeingOrderableWhenTheStationIsSwitchedOffAsync(
    GastronomyAppDbContext dbContext,
    Guid stationId,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    var nowUtc = _clock.UtcNow;

    List<Guid> festivalIdsStillToCome = await dbContext.Festivals
                                                       .AsNoTracking()
                                                       .Where(festival => !festival.IsHidden
                                                                          && festival.EndsAtUtc > nowUtc)
                                                       .Select(festival => festival.Id)
                                                       .ToListAsync(cancellationToken);

    HashSet<Guid> stranded = [];

    foreach (var festivalId in festivalIdsStillToCome)
    {
      stranded.UnionWith(await WouldStopBeingOrderableAtAsync(dbContext,
                                                              festivalId,
                                                              [stationId],
                                                              cancellationToken));
    }

    return [.. stranded];
  }

  private async Task<IReadOnlyList<Guid>> IdsAtAsync(GastronomyAppDbContext dbContext,
                                                     Guid festivalId,
                                                     IReadOnlyCollection<Guid> stationsNoLongerPreparing,
                                                     CancellationToken cancellationToken)
  {
    List<Guid> stationsPreparing = await StationsPreparingAtAsync(dbContext,
                                                                  festivalId,
                                                                  stationsNoLongerPreparing,
                                                                  cancellationToken);

    List<Guid> itemIdsWithAStation = await dbContext.ItemStationAssignments
                                                    .AsNoTracking()
                                                    .Where(assignment => assignment.FestivalId == festivalId
                                                                         && stationsPreparing.Contains(assignment.StationId))
                                                    .Select(assignment => assignment.CatalogItemId)
                                                    .Distinct()
                                                    .ToListAsync(cancellationToken);

    return
    [
      .. await (from menuRow in dbContext.FestivalCatalogItems.AsNoTracking()
                join item in dbContext.CatalogItems.AsNoTracking()
                  on menuRow.CatalogItemId equals item.Id
                where menuRow.FestivalId == festivalId
                      && item.IsActive
                      && itemIdsWithAStation.Contains(item.Id)
                select item.Id)
       .ToListAsync(cancellationToken)
    ];
  }

  private async Task<List<Guid>> StationsPreparingAtAsync(GastronomyAppDbContext dbContext,
                                                          Guid festivalId,
                                                          IReadOnlyCollection<Guid> stationsNoLongerPreparing,
                                                          CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    List<Guid> gone = [.. stationsNoLongerPreparing];

    return await (from link in dbContext.FestivalStations.AsNoTracking()
                  join station in dbContext.Stations.AsNoTracking()
                    on link.StationId equals station.Id
                  where link.FestivalId == festivalId
                        && station.IsActive
                        && !gone.Contains(station.Id)
                  select station.Id)
                 .ToListAsync(cancellationToken);
  }
}
