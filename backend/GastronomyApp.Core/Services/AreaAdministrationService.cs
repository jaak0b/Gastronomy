using ErrorOr;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class AreaAdministrationService
{
  private readonly IFestivalRepository _festivalRepository;
  private readonly ITablePlanRepository _repository;
  private readonly RunningFestivalLookup _runningFestival;

  public AreaAdministrationService(ITablePlanRepository repository, IFestivalRepository festivalRepository, RunningFestivalLookup runningFestival)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
    _runningFestival = runningFestival;
  }

  public async Task<ErrorOr<IReadOnlyList<Area>>> ListTablePlanAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    if (!await _festivalRepository.ExistsAsync(festivalId, cancellationToken))
      return Refusal.TablePlan.FestivalNotFound(festivalId);

    IReadOnlyList<Area> areas = await _repository.FindTablePlanAsync(festivalId, cancellationToken);

    return areas.ToErrorOr();
  }

  public async Task<ErrorOr<Area>> CreateAsync(Guid festivalId, SaveAreaRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (!await _festivalRepository.ExistsAsync(festivalId, cancellationToken))
      return Refusal.TablePlan.FestivalNotFound(festivalId);

    if (await _repository.IsAreaNameTakenAsync(festivalId, request.Name!, null, cancellationToken))
      return Refusal.TablePlan.AreaNameTaken(festivalId, request.Name!);

    Area created = new()
                   {
                     Id = Guid.NewGuid(),
                     FestivalId = festivalId,
                     Name = request.Name!,
                     SortOrder = request.SortOrder,
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
    area.SortOrder = request.SortOrder;
    await _repository.SaveChangesAsync(cancellationToken);

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

    if (_runningFestival.IsRunning(festival) && await _repository.HasItemsToHandOutOrSettleInAreaAsync(areaId, cancellationToken))
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

  public async Task<ErrorOr<Area>> DeleteAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken)
  {
    var area = await _repository.FindAreaWithContentsAsync(festivalId, areaId, cancellationToken);

    if (area is null)
      return Refusal.TablePlan.AreaNotFound(festivalId, areaId);

    var festival = await _festivalRepository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Refusal.TablePlan.FestivalNotFound(festivalId);

    if (_runningFestival.IsRunning(festival))
      return Refusal.TablePlan.AreaStaysWhileTheFestivalRuns(festivalId, areaId);

    if (await _repository.HasOrdersInAreaAsync(areaId, cancellationToken))
      return Refusal.TablePlan.AreaWithOrdersStays(festivalId, areaId);

    _repository.RemoveStationShapes(area.StationShapes.ToList());
    _repository.RemoveTables(area.Tables.ToList());
    _repository.RemoveArea(area);
    await _repository.SaveChangesAsync(cancellationToken);

    return area;
  }
}
