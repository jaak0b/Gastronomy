using ErrorOr;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Refusals;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class AreaEditService
{
  private readonly IGastronomyDatabase _database;
  private readonly TypeAdapterConfig _mapping;
  private readonly SortOrdering _ordering;
  private readonly FestivalRowHiding _hiding;

  public AreaEditService(IGastronomyDatabase database, TypeAdapterConfig mapping, SortOrdering ordering, FestivalRowHiding hiding)
  {
    _database = database;
    _mapping = mapping;
    _ordering = ordering;
    _hiding = hiding;
  }

  public async Task<ErrorOr<SavedAreaView>> CreateAsync(Guid festivalId, SaveAreaRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (!await _database.Festivals.AsNoTracking().AnyAsync(row => row.Id == festivalId, cancellationToken))
      return Refusal.TablePlan.FestivalNotFound(festivalId);

    if (await NameIsTakenAsync(festivalId, request.Name!, null, cancellationToken))
      return Refusal.TablePlan.AreaNameTaken(festivalId, request.Name!);

    var takenSortOrders = await _database.Areas.AsNoTracking().Where(area => area.FestivalId == festivalId).Select(area => area.SortOrder).ToListAsync(cancellationToken);

    Area created = new()
                   {
                     Id = Guid.NewGuid(),
                     FestivalId = festivalId,
                     Name = request.Name!,
                     SortOrder = _ordering.NextSortOrder(takenSortOrders),
                     IsHidden = false
                   };

    _database.Areas.Add(created);
    await _database.SaveChangesAsync(cancellationToken);

    return created.Adapt<SavedAreaView>(_mapping);
  }

  public async Task<ErrorOr<SavedAreaView>> UpdateAsync(Guid festivalId, Guid areaId, SaveAreaRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var area = await FindAreaAsync(festivalId, areaId, cancellationToken);

    if (area is null)
      return Refusal.TablePlan.AreaNotFound(festivalId, areaId);

    if (await NameIsTakenAsync(festivalId, request.Name!, areaId, cancellationToken))
      return Refusal.TablePlan.AreaNameTaken(festivalId, request.Name!);

    area.Name = request.Name!;
    await _database.SaveChangesAsync(cancellationToken);

    return area.Adapt<SavedAreaView>(_mapping);
  }

  public async Task<ErrorOr<SavedAreaView>> MoveAsync(Guid festivalId, Guid areaId, MoveAreaRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    IReadOnlyList<Area> areas = await _database.Areas.Where(area => area.FestivalId == festivalId).OrderBy(area => area.SortOrder).ThenBy(area => area.Name).ToListAsync(cancellationToken);
    var moved = areas.FirstOrDefault(candidate => candidate.Id == areaId);

    if (moved is null)
      return Refusal.TablePlan.AreaNotFound(festivalId, areaId);

    var reordered = _ordering.Move(areas, areaId, request.Direction, neighbour => request.ShowsHiddenAreas || !neighbour.IsHidden);

    if (!_ordering.IsNumberedInOrder(reordered))
    {
      _ordering.NumberInOrder(reordered);
      await _database.SaveChangesAsync(cancellationToken);
    }

    return moved.Adapt<SavedAreaView>(_mapping);
  }

  public async Task<ErrorOr<SavedAreaView>> HideAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken)
  {
    return await _hiding.HideAsync<Area, SavedAreaView>(festivalId, areaId, item => item.StationOrder.Order.Table.AreaId == areaId, id => Refusal.TablePlan.AreaNotFound(festivalId, id), id => Refusal.TablePlan.AreaWithItemsToHandOutOrSettleStaysShown(festivalId, id), cancellationToken);
  }

  private async Task<Area?> FindAreaAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken)
  {
    return await _database.Areas.FirstOrDefaultAsync(area => area.Id == areaId && area.FestivalId == festivalId, cancellationToken);
  }

  private async Task<bool> NameIsTakenAsync(Guid festivalId, string name, Guid? ownAreaId, CancellationToken cancellationToken)
  {
    return await _database.Areas.AsNoTracking().Where(area => area.FestivalId == festivalId).Namesakes(name, ownAreaId).AnyAsync(cancellationToken);
  }
}
