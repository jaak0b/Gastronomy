using ErrorOr;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class TableAdministrationService
{
  private readonly AreaShapeService _areaShapeService;
  private readonly IFestivalRepository _festivalRepository;
  private readonly ItemsToHandOutLookup _itemsToHandOut;
  private readonly ITablePlanRepository _repository;
  private readonly TableCopyNaming _tableCopyNaming;

  public TableAdministrationService(ITablePlanRepository repository, IFestivalRepository festivalRepository, ItemsToHandOutLookup itemsToHandOut, AreaShapeService areaShapeService, TableCopyNaming tableCopyNaming)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
    _itemsToHandOut = itemsToHandOut;
    _areaShapeService = areaShapeService;
    _tableCopyNaming = tableCopyNaming;
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

  public async Task<ErrorOr<IReadOnlyList<Table>>> CopyAsync(Guid festivalId, CopyTablesRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (await _repository.FindAreaAsync(festivalId, request.AreaId, cancellationToken) is null)
      return Refusal.TablePlan.AreaOutsideTheFestival(festivalId, request.AreaId);

    if (request.Tables is null || request.Tables.Count == 0)
      return Refusal.TablePlan.TableCopyWithoutSourceTables(festivalId);

    List<Guid> sourceTableIds = request.Tables.Select(table => table.SourceTableId).ToList();

    if (sourceTableIds.Distinct().Count() != sourceTableIds.Count)
      return Refusal.TablePlan.TableCopyRepeatsASourceTable(festivalId, sourceTableIds.First(id => sourceTableIds.Count(candidate => candidate == id) > 1));

    IReadOnlyList<Table> sources = await _repository.FindTablesAsync(festivalId, sourceTableIds, cancellationToken);

    if (sources.Count != sourceTableIds.Count)
      return Refusal.TablePlan.TableNotFound(festivalId, sourceTableIds.First(id => sources.All(source => source.Id != id)));

    IReadOnlyList<TableCopyName> names = _tableCopyNaming.NamesFor(sources, await _repository.FindTableNamesAsync(festivalId, cancellationToken));
    var tooLong = names.FirstOrDefault(copy => copy.Name.Length > TableCopyNaming.LongestTableName);

    if (tooLong is not null)
      return Refusal.TablePlan.TableCopyNameTooLong(festivalId, sources.Single(source => source.Id == tooLong.SourceTableId).Name, tooLong.Name);

    Dictionary<Guid, string> nameBySourceTableId = names.ToDictionary(copy => copy.SourceTableId, copy => copy.Name);
    List<Table> copies = [];

    foreach (var requested in request.Tables)
    {
      var source = sources.Single(table => table.Id == requested.SourceTableId);
      Table created = new()
                      {
                        Id = Guid.NewGuid(),
                        FestivalId = festivalId,
                        AreaId = request.AreaId,
                        Name = nameBySourceTableId[source.Id],
                        IsHidden = false
                      };

      _areaShapeService.Reshape(created, requested.Geometry!);

      await _repository.AddTableAsync(created, cancellationToken);
      copies.Add(created);
    }

    await _repository.SaveChangesAsync(cancellationToken);

    return copies;
  }

  public async Task<ErrorOr<Success>> MoveAsync(Guid festivalId, MoveTablesRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (request.Tables is null || request.Tables.Count == 0)
      return Refusal.TablePlan.TableMoveWithoutTables(festivalId);

    List<Guid> tableIds = request.Tables.Select(table => table.TableId).ToList();

    if (tableIds.Distinct().Count() != tableIds.Count)
      return Refusal.TablePlan.TableMoveRepeatsATable(festivalId, tableIds.First(id => tableIds.Count(candidate => candidate == id) > 1));

    IReadOnlyList<Table> tables = await _repository.FindTablesAsync(festivalId, tableIds, cancellationToken);

    if (tables.Count != tableIds.Count)
      return Refusal.TablePlan.TableNotFound(festivalId, tableIds.First(id => tables.All(table => table.Id != id)));

    foreach (var requested in request.Tables)
    {
      _areaShapeService.Reshape(tables.Single(table => table.Id == requested.TableId), requested.Geometry!);
    }

    await _repository.SaveChangesAsync(cancellationToken);

    return Result.Success;
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

    if (table.IsHidden)
      return table;

    var festival = await _festivalRepository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Refusal.TablePlan.FestivalNotFound(festivalId);

    if (await _itemsToHandOut.HasItemsToHandOutOrSettleAtTableIfRunningAsync(festival, tableId, cancellationToken))
      return Refusal.TablePlan.TableWithItemsToHandOutOrSettleStaysShown(festivalId, tableId);

    table.IsHidden = true;
    await _repository.SaveChangesAsync(cancellationToken);

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
}
