using ErrorOr;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class TableAdministrationService
{
  private readonly AreaShapeService _areaShapeService;
  private readonly IFestivalRepository _festivalRepository;
  private readonly ITablePlanRepository _repository;
  private readonly RunningFestivalLookup _runningFestival;

  public TableAdministrationService(ITablePlanRepository repository, IFestivalRepository festivalRepository, RunningFestivalLookup runningFestival, AreaShapeService areaShapeService)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
    _runningFestival = runningFestival;
    _areaShapeService = areaShapeService;
  }

  public async Task<ErrorOr<Table>> CreateAsync(Guid festivalId, SaveTableRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (await _repository.FindAreaAsync(festivalId, request.AreaId, cancellationToken) is null)
      return Refusal.TablePlan.AreaOutsideTheFestival(festivalId, request.AreaId);

    if (await _repository.IsTableNameTakenAsync(festivalId, request.Name!, null, cancellationToken))
      return Refusal.TablePlan.TableNameTaken(festivalId, request.Name!);

    Table created = new()
                    {
                      Id = Guid.NewGuid(),
                      FestivalId = festivalId,
                      AreaId = request.AreaId,
                      Name = request.Name!,
                      IsHidden = false
                    };

    _areaShapeService.Reshape(created, request.Geometry!);

    await _repository.AddTableAsync(created, cancellationToken);
    await _repository.SaveChangesAsync(cancellationToken);

    return created;
  }

  public async Task<ErrorOr<Table>> UpdateAsync(Guid festivalId, Guid tableId, SaveTableRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var table = await _repository.FindTableAsync(festivalId, tableId, cancellationToken);

    if (table is null)
      return Refusal.TablePlan.TableNotFound(festivalId, tableId);

    if (await _repository.FindAreaAsync(festivalId, request.AreaId, cancellationToken) is null)
      return Refusal.TablePlan.AreaOutsideTheFestival(festivalId, request.AreaId);

    if (await _repository.IsTableNameTakenAsync(festivalId, request.Name!, tableId, cancellationToken))
      return Refusal.TablePlan.TableNameTaken(festivalId, request.Name!);

    table.AreaId = request.AreaId;
    table.Name = request.Name!;
    _areaShapeService.Reshape(table, request.Geometry!);
    await _repository.SaveChangesAsync(cancellationToken);

    return table;
  }

  public async Task<ErrorOr<Table>> HideAsync(Guid festivalId, Guid tableId, CancellationToken cancellationToken)
  {
    var table = await _repository.FindTableAsync(festivalId, tableId, cancellationToken);

    if (table is null)
      return Refusal.TablePlan.TableNotFound(festivalId, tableId);

    if (!table.IsHidden)
    {
      table.IsHidden = true;
      await _repository.SaveChangesAsync(cancellationToken);
    }

    return table;
  }

  public async Task<ErrorOr<Table>> ShowAsync(Guid festivalId, Guid tableId, CancellationToken cancellationToken)
  {
    var table = await _repository.FindTableAsync(festivalId, tableId, cancellationToken);

    if (table is null)
      return Refusal.TablePlan.TableNotFound(festivalId, tableId);

    if (table.IsHidden)
    {
      table.IsHidden = false;
      await _repository.SaveChangesAsync(cancellationToken);
    }

    return table;
  }

  public async Task<ErrorOr<Table>> DeleteAsync(Guid festivalId, Guid tableId, CancellationToken cancellationToken)
  {
    var table = await _repository.FindTableAsync(festivalId, tableId, cancellationToken);

    if (table is null)
      return Refusal.TablePlan.TableNotFound(festivalId, tableId);

    var festival = await _festivalRepository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Refusal.TablePlan.FestivalNotFound(festivalId);

    if (_runningFestival.IsRunning(festival))
      return Refusal.TablePlan.TableStaysWhileTheFestivalRuns(festivalId, tableId);

    _repository.RemoveTable(table);
    await _repository.SaveChangesAsync(cancellationToken);

    return table;
  }
}
