using ErrorOr;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class FestivalStationService
{
  private readonly AreaShapeService _areaShapeService;
  private readonly IGastronomyDatabase _database;
  private readonly ItemsToHandOutLookup _itemsToHandOut;
  private readonly TypeAdapterConfig _mapping;
  private readonly SequenceNumberAllocator _numberAllocator;
  private readonly ItemOrderability _orderability;

  public FestivalStationService(IGastronomyDatabase database, TypeAdapterConfig mapping, ItemOrderability orderability, SequenceNumberAllocator numberAllocator, ItemsToHandOutLookup itemsToHandOut, AreaShapeService areaShapeService)
  {
    _database = database;
    _mapping = mapping;
    _orderability = orderability;
    _numberAllocator = numberAllocator;
    _itemsToHandOut = itemsToHandOut;
    _areaShapeService = areaShapeService;
  }

  public async Task<ErrorOr<SavedStationView>> AddAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    if (!await _database.Festivals.AsNoTracking().AnyAsync(festival => festival.Id == festivalId, cancellationToken))
      return Refusal.FestivalStation.FestivalNotFound(festivalId);

    if (!await _database.Stations.AsNoTracking().AnyAsync(station => station.Id == stationId, cancellationToken))
      return Refusal.FestivalStation.StationNotFound(stationId);

    if (await FindLinkAsync(festivalId, stationId, cancellationToken) is { } alreadyTakingPart)
      return alreadyTakingPart.Adapt<SavedStationView>(_mapping);

    var nextStationOrderNumber = await _numberAllocator.FindNextStationOrderNumberAsync(festivalId, stationId, cancellationToken);

    FestivalStation link = new()
                           {
                             Id = Guid.NewGuid(),
                             FestivalId = festivalId,
                             StationId = stationId,
                             NextStationOrderNumber = nextStationOrderNumber
                           };

    _database.FestivalStations.Add(link);
    await _database.SaveChangesAsync(cancellationToken);

    return link.Adapt<SavedStationView>(_mapping);
  }

  public async Task<ErrorOr<Success>> RemoveAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    var link = await FindLinkAsync(festivalId, stationId, cancellationToken);

    if (link is null)
      return Refusal.FestivalStation.StationLinkNotFound(festivalId, stationId);

    var festival = await _database.Festivals.AsNoTracking().FirstOrDefaultAsync(row => row.Id == festivalId, cancellationToken);

    if (festival is null)
      return Refusal.FestivalStation.FestivalNotFound(festivalId);

    if ((await _itemsToHandOut.FindStationIdsIfRunningAsync(festival, cancellationToken)).Contains(stationId))
      return Refusal.FestivalStation.StationHasUnfulfilledItems(festivalId, stationId);

    IReadOnlyList<Guid> strandedItemIds = await _orderability.FindItemsStrandedByRemovingStationAsync(festivalId, stationId, cancellationToken);

    if (strandedItemIds.Count > 0)
      return Refusal.FestivalStation.ItemsWouldHaveNoStation(strandedItemIds.Count);

    List<ItemStationAssignment> assignmentsHere = await _database.ItemStationAssignments.Where(assignment => assignment.FestivalId == festivalId && assignment.StationId == stationId).ToListAsync(cancellationToken);

    if (await FindShapeAsync(link.Id, cancellationToken) is { } shape)
      _database.FestivalStationShapes.Remove(shape);

    _database.ItemStationAssignments.RemoveRange(assignmentsHere);
    _database.FestivalStations.Remove(link);

    await _database.SaveChangesAsync(cancellationToken);

    return Result.Success;
  }

  public async Task<ErrorOr<SavedStationView>> PlaceShapeAsync(Guid festivalId, Guid stationId, SaveStationShapeRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var link = await FindLinkAsync(festivalId, stationId, cancellationToken);

    if (link is null)
      return Refusal.TablePlan.StationLinkNotFound(festivalId, stationId);

    if (!await _database.Areas.AsNoTracking().AnyAsync(area => area.Id == request.AreaId && area.FestivalId == festivalId, cancellationToken))
      return Refusal.TablePlan.AreaOutsideTheFestival(festivalId, request.AreaId);

    var shape = await FindShapeAsync(link.Id, cancellationToken);

    if (shape is null)
    {
      shape = new()
              {
                FestivalStationId = link.Id,
                FestivalId = festivalId,
                AreaId = request.AreaId
              };

      _database.FestivalStationShapes.Add(shape);
    }
    else
      shape.AreaId = request.AreaId;

    _areaShapeService.Reshape(shape, request.Geometry!);
    await _database.SaveChangesAsync(cancellationToken);

    return link.Adapt<SavedStationView>(_mapping);
  }

  public async Task<ErrorOr<Success>> RemoveShapeAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    var link = await FindLinkAsync(festivalId, stationId, cancellationToken);

    if (link is null)
      return Refusal.TablePlan.StationLinkNotFound(festivalId, stationId);

    var shape = await FindShapeAsync(link.Id, cancellationToken);

    if (shape is null)
      return Refusal.TablePlan.StationShapeNotFound(festivalId, stationId);

    _database.FestivalStationShapes.Remove(shape);
    await _database.SaveChangesAsync(cancellationToken);

    return Result.Success;
  }

  private async Task<FestivalStation?> FindLinkAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return await _database.FestivalStations.FirstOrDefaultAsync(link => link.FestivalId == festivalId && link.StationId == stationId, cancellationToken);
  }

  private async Task<FestivalStationShape?> FindShapeAsync(Guid festivalStationId, CancellationToken cancellationToken)
  {
    return await _database.FestivalStationShapes.FirstOrDefaultAsync(shape => shape.FestivalStationId == festivalStationId, cancellationToken);
  }
}
