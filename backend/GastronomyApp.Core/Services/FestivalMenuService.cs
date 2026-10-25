using ErrorOr;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class FestivalMenuService
{
  private readonly IGastronomyDatabase _database;
  private readonly TypeAdapterConfig _mapping;
  private readonly ItemOrderability _orderability;
  private readonly RunningFestivalLookup _runningFestival;

  public FestivalMenuService(IGastronomyDatabase database, TypeAdapterConfig mapping, ItemOrderability orderability, RunningFestivalLookup runningFestival)
  {
    _database = database;
    _mapping = mapping;
    _orderability = orderability;
    _runningFestival = runningFestival;
  }

  public async Task<ErrorOr<SavedItemView>> PutOnTheMenuAsync(Guid festivalId, Guid catalogItemId, SaveFestivalItemRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (!await _database.Festivals.AsNoTracking().AnyAsync(festival => festival.Id == festivalId, cancellationToken))
      return Refusal.FestivalMenu.FestivalNotFound(festivalId);

    if (!await _database.CatalogItems.AsNoTracking().AnyAsync(item => item.Id == catalogItemId, cancellationToken))
      return Refusal.FestivalMenu.CatalogItemNotFound(catalogItemId);

    List<Guid> stationIds = (request.StationIds ?? []).ToList();

    var stationIdsAtTheFestival = await _database.FestivalStations.AsNoTracking().Where(link => link.FestivalId == festivalId).Select(link => link.StationId).ToListAsync(cancellationToken);

    List<Guid> strangers = stationIds.Where(stationId => !stationIdsAtTheFestival.Contains(stationId)).ToList();

    if (strangers.Count > 0)
      return Refusal.FestivalMenu.StationsDoNotBelongToTheFestival(festivalId, catalogItemId, strangers);

    if (!await _orderability.AnyOfTheseStationsPreparesAtAsync(festivalId, stationIds, cancellationToken))
      return Refusal.FestivalMenu.NoStationPreparesTheItem(catalogItemId);

    var menuRow = await FindMenuRowAsync(festivalId, catalogItemId, cancellationToken);

    if (menuRow is null)
    {
      menuRow = new()
                {
                  Id = Guid.NewGuid(),
                  FestivalId = festivalId,
                  CatalogItemId = catalogItemId,
                  PriceCents = request.PriceCents,
                  IsAvailable = true
                };

      _database.FestivalCatalogItems.Add(menuRow);
    }
    else
      menuRow.PriceCents = request.PriceCents;

    _database.ItemStationAssignments.RemoveRange(await FindAssignmentsAsync(festivalId, catalogItemId, cancellationToken));

    foreach (var stationId in stationIds.Distinct())
      _database.ItemStationAssignments.Add(new()
                                           {
                                             Id = Guid.NewGuid(),
                                             FestivalId = festivalId,
                                             CatalogItemId = catalogItemId,
                                             StationId = stationId
                                           });

    await _database.SaveChangesAsync(cancellationToken);

    return menuRow.Adapt<SavedItemView>(_mapping);
  }

  public async Task<ErrorOr<Success>> TakeOffTheMenuAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken)
  {
    var menuRow = await FindMenuRowAsync(festivalId, catalogItemId, cancellationToken);

    if (menuRow is null)
      return Refusal.FestivalMenu.MenuRowNotFound(festivalId, catalogItemId);

    var festival = await _database.Festivals.AsNoTracking().FirstOrDefaultAsync(row => row.Id == festivalId, cancellationToken);

    if (festival is null)
      return Refusal.FestivalMenu.FestivalNotFound(festivalId);

    if (_runningFestival.IsRunning(festival))
      return Refusal.FestivalMenu.FestivalIsRunning(festivalId);

    _database.ItemStationAssignments.RemoveRange(await FindAssignmentsAsync(festivalId, catalogItemId, cancellationToken));
    _database.FestivalCatalogItems.Remove(menuRow);

    await _database.SaveChangesAsync(cancellationToken);

    return Result.Success;
  }

  public async Task<ErrorOr<SavedItemView>> SetAvailabilityAsync(Guid festivalId, Guid catalogItemId, SetAvailabilityRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var menuRow = await FindMenuRowAsync(festivalId, catalogItemId, cancellationToken);

    if (menuRow is null)
      return Refusal.FestivalMenu.MenuRowNotFound(festivalId, catalogItemId);

    if (menuRow.IsAvailable != request.IsAvailable)
    {
      menuRow.IsAvailable = request.IsAvailable;
      await _database.SaveChangesAsync(cancellationToken);
    }

    return menuRow.Adapt<SavedItemView>(_mapping);
  }

  private async Task<FestivalCatalogItem?> FindMenuRowAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken)
  {
    return await _database.FestivalCatalogItems.FirstOrDefaultAsync(menuRow => menuRow.FestivalId == festivalId && menuRow.CatalogItemId == catalogItemId, cancellationToken);
  }

  private async Task<List<ItemStationAssignment>> FindAssignmentsAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken)
  {
    return await _database.ItemStationAssignments.Where(assignment => assignment.FestivalId == festivalId && assignment.CatalogItemId == catalogItemId).ToListAsync(cancellationToken);
  }
}
