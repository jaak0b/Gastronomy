using ErrorOr;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class AreaAdministrationService
{
  private readonly IFestivalRepository _festivalRepository;
  private readonly ItemsToHandOutLookup _itemsToHandOut;
  private readonly SortOrdering _ordering;
  private readonly ITablePlanRepository _repository;

  public AreaAdministrationService(ITablePlanRepository repository, IFestivalRepository festivalRepository, ItemsToHandOutLookup itemsToHandOut, SortOrdering ordering)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
    _itemsToHandOut = itemsToHandOut;
    _ordering = ordering;
  }

  public async Task<ErrorOr<IReadOnlyList<ListedArea>>> ListTablePlanAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var festival = await _festivalRepository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Refusal.TablePlan.FestivalNotFound(festivalId);

    IReadOnlyList<ListedArea> areas = await _itemsToHandOut.FindTablePlanAsync(festival, cancellationToken);

    return areas.ToErrorOr();
  }

  public async Task<ErrorOr<Area>> CreateAsync(Guid festivalId, SaveAreaRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (!await _festivalRepository.ExistsAsync(festivalId, cancellationToken))
      return Refusal.TablePlan.FestivalNotFound(festivalId);

    if (await _repository.IsAreaNameTakenAsync(festivalId, request.Name!, null, cancellationToken))
      return Refusal.TablePlan.AreaNameTaken(festivalId, request.Name!);

    IReadOnlyList<Area> areas = await _repository.FindAreasInOrderAsync(festivalId, cancellationToken);

    Area created = new()
                   {
                     Id = Guid.NewGuid(),
                     FestivalId = festivalId,
                     Name = request.Name!,
                     SortOrder = _ordering.NextSortOrder(areas.Select(area => area.SortOrder).ToList()),
                     IsHidden = false
                   };

    await _repository.AddAreaAsync(created, cancellationToken);
    await _repository.SaveChangesAsync(cancellationToken);

    return created;
  }

  public async Task<ErrorOr<Area>> UpdateAsync(Guid festivalId, Guid areaId, SaveAreaRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var area = await _repository.FindAreaAsync(festivalId, areaId, cancellationToken);

    if (area is null)
      return Refusal.TablePlan.AreaNotFound(festivalId, areaId);

    if (await _repository.IsAreaNameTakenAsync(festivalId, request.Name!, areaId, cancellationToken))
      return Refusal.TablePlan.AreaNameTaken(festivalId, request.Name!);

    area.Name = request.Name!;
    await _repository.SaveChangesAsync(cancellationToken);

    return area;
  }

  public async Task<ErrorOr<Area>> MoveAsync(Guid festivalId, Guid areaId, MoveAreaRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    IReadOnlyList<Area> areas = await _repository.FindAreasInOrderAsync(festivalId, cancellationToken);
    var area = areas.FirstOrDefault(candidate => candidate.Id == areaId);

    if (area is null)
      return Refusal.TablePlan.AreaNotFound(festivalId, areaId);

    IReadOnlyList<Area> reordered = _ordering.Move(areas, areaId, request.Direction, neighbour => request.ShowsHiddenAreas || !neighbour.IsHidden);

    if (!_ordering.IsNumberedInOrder(reordered))
    {
      _ordering.NumberInOrder(reordered);
      await _repository.SaveChangesAsync(cancellationToken);
    }

    return area;
  }

  public async Task<ErrorOr<Area>> HideAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken)
  {
    var area = await _repository.FindAreaAsync(festivalId, areaId, cancellationToken);

    if (area is null)
      return Refusal.TablePlan.AreaNotFound(festivalId, areaId);

    if (area.IsHidden)
      return area;

    var festival = await _festivalRepository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Refusal.TablePlan.FestivalNotFound(festivalId);

    if (await _itemsToHandOut.HasItemsToHandOutOrSettleInAreaIfRunningAsync(festival, areaId, cancellationToken))
      return Refusal.TablePlan.AreaWithItemsToHandOutOrSettleStaysShown(festivalId, areaId);

    area.IsHidden = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return area;
  }

  public async Task<ErrorOr<Area>> ShowAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken)
  {
    var area = await _repository.FindAreaAsync(festivalId, areaId, cancellationToken);

    if (area is null)
      return Refusal.TablePlan.AreaNotFound(festivalId, areaId);

    if (area.IsHidden)
    {
      area.IsHidden = false;
      await _repository.SaveChangesAsync(cancellationToken);
    }

    return area;
  }
}
