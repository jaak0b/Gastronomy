using ErrorOr;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Refusals;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class TableEditService
{
  private readonly AreaShapeService _areaShapeService;
  private readonly IGastronomyDatabase _database;
  private readonly TypeAdapterConfig _mapping;
  private readonly FestivalRowHiding _hiding;
  private readonly TableCopyNaming _tableCopyNaming;

  public TableEditService(IGastronomyDatabase database, TypeAdapterConfig mapping, FestivalRowHiding hiding, AreaShapeService areaShapeService, TableCopyNaming tableCopyNaming)
  {
    _database = database;
    _mapping = mapping;
    _hiding = hiding;
    _areaShapeService = areaShapeService;
    _tableCopyNaming = tableCopyNaming;
  }

  public async Task<ErrorOr<SavedTableView>> CreateAsync(Guid festivalId, SaveTableRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (!await AreaBelongsToFestivalAsync(festivalId, request.AreaId, cancellationToken))
      return Refusal.TablePlan.AreaOutsideTheFestival(festivalId, request.AreaId);

    if (await NameRefusalAsync(festivalId, request.Name!, null, cancellationToken) is { } nameRefusal)
      return nameRefusal;

    Table created = new()
                    {
                      Id = Guid.NewGuid(),
                      FestivalId = festivalId,
                      AreaId = request.AreaId,
                      Name = request.Name!,
                      IsHidden = false
                    };

    _areaShapeService.Reshape(created, request.Geometry!);

    _database.Tables.Add(created);
    await _database.SaveChangesAsync(cancellationToken);

    return created.Adapt<SavedTableView>(_mapping);
  }

  public async Task<ErrorOr<CopiedTablesView>> CopyAsync(Guid festivalId, CopyTablesRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (!await AreaBelongsToFestivalAsync(festivalId, request.AreaId, cancellationToken))
      return Refusal.TablePlan.AreaOutsideTheFestival(festivalId, request.AreaId);

    if (request.Tables is null || request.Tables.Count == 0)
      return Refusal.TablePlan.TableCopyWithoutSourceTables(festivalId);

    var sourceTableIds = request.Tables.Select(table => table.SourceTableId).ToList();

    if (sourceTableIds.Distinct().Count() != sourceTableIds.Count)
      return Refusal.TablePlan.TableCopyRepeatsASourceTable(festivalId, sourceTableIds.First(id => sourceTableIds.Count(candidate => candidate == id) > 1));

    IReadOnlyList<Table> sources = await _database.Tables.AsNoTracking().WhereFestivalIs(festivalId).Where(table => sourceTableIds.Contains(table.Id)).ToListAsync(cancellationToken);

    if (sources.Count != sourceTableIds.Count)
      return Refusal.TablePlan.TableNotFound(festivalId, sourceTableIds.First(id => sources.All(source => source.Id != id)));

    IReadOnlyList<string> takenNames = await _database.Tables.AsNoTracking().WhereFestivalIs(festivalId).Select(table => table.Name).ToListAsync(cancellationToken);
    var names = _tableCopyNaming.NamesFor(sources, takenNames);
    var tooLong = names.FirstOrDefault(copy => copy.Name.Length > TableCopyNaming.LongestTableName);

    if (tooLong is not null)
      return Refusal.TablePlan.TableCopyNameTooLong(festivalId, sources.Single(source => source.Id == tooLong.SourceTableId).Name, tooLong.Name);

    var nameBySourceTableId = names.ToDictionary(copy => copy.SourceTableId, copy => copy.Name);
    List<Table> copies = [];

    foreach (var requested in request.Tables)
    {
      Table created = new()
                      {
                        Id = Guid.NewGuid(),
                        FestivalId = festivalId,
                        AreaId = request.AreaId,
                        Name = nameBySourceTableId[requested.SourceTableId],
                        IsHidden = false
                      };

      _areaShapeService.Reshape(created, requested.Geometry!);

      _database.Tables.Add(created);
      copies.Add(created);
    }

    await _database.SaveChangesAsync(cancellationToken);

    return new CopiedTablesView(copies.Select(copy => copy.Id).ToList());
  }

  public async Task<ErrorOr<Success>> MoveAsync(Guid festivalId, MoveTablesRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (request.Tables is null || request.Tables.Count == 0)
      return Refusal.TablePlan.TableMoveWithoutTables(festivalId);

    var tableIds = request.Tables.Select(table => table.TableId).ToList();

    if (tableIds.Distinct().Count() != tableIds.Count)
      return Refusal.TablePlan.TableMoveRepeatsATable(festivalId, tableIds.First(id => tableIds.Count(candidate => candidate == id) > 1));

    IReadOnlyList<Table> tables = await _database.Tables.WhereFestivalIs(festivalId).Where(table => tableIds.Contains(table.Id)).ToListAsync(cancellationToken);

    if (tables.Count != tableIds.Count)
      return Refusal.TablePlan.TableNotFound(festivalId, tableIds.First(id => tables.All(table => table.Id != id)));

    foreach (var requested in request.Tables)
      _areaShapeService.Reshape(tables.Single(table => table.Id == requested.TableId), requested.Geometry!);

    await _database.SaveChangesAsync(cancellationToken);

    return Result.Success;
  }

  public async Task<ErrorOr<SavedTableView>> UpdateAsync(Guid festivalId, Guid tableId, SaveTableRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var table = await FindTableAsync(festivalId, tableId, cancellationToken);

    if (table is null)
      return Refusal.TablePlan.TableNotFound(festivalId, tableId);

    if (!await AreaBelongsToFestivalAsync(festivalId, request.AreaId, cancellationToken))
      return Refusal.TablePlan.AreaOutsideTheFestival(festivalId, request.AreaId);

    if (await NameRefusalAsync(festivalId, request.Name!, tableId, cancellationToken) is { } nameRefusal)
      return nameRefusal;

    table.AreaId = request.AreaId;
    table.Name = request.Name!;
    _areaShapeService.Reshape(table, request.Geometry!);
    await _database.SaveChangesAsync(cancellationToken);

    return table.Adapt<SavedTableView>(_mapping);
  }

  public async Task<ErrorOr<SavedTableView>> HideAsync(Guid festivalId, Guid tableId, CancellationToken cancellationToken)
  {
    return await _hiding.HideAsync<Table, SavedTableView>(festivalId, tableId, item => item.StationOrder.Order.TableId == tableId, id => Refusal.TablePlan.TableNotFound(festivalId, id), id => Refusal.TablePlan.TableWithItemsToHandOutOrSettleStaysShown(festivalId, id), cancellationToken);
  }

  private async Task<Table?> FindTableAsync(Guid festivalId, Guid tableId, CancellationToken cancellationToken)
  {
    return await _database.Tables.WhereFestivalIs(festivalId).FirstOrDefaultAsync(table => table.Id == tableId, cancellationToken);
  }

  private async Task<bool> AreaBelongsToFestivalAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken)
  {
    return await _database.Areas.AsNoTracking().WhereFestivalIs(festivalId).AnyAsync(area => area.Id == areaId, cancellationToken);
  }

  private async Task<Error?> NameRefusalAsync(Guid festivalId, string name, Guid? ownTableId, CancellationToken cancellationToken)
  {
    var namesake = await _database.Tables.AsNoTracking().WhereFestivalIs(festivalId).Namesakes(name, ownTableId).Select(table => new TableNamesake(table.Area.Name, table.IsHidden)).SingleOrDefaultAsync(cancellationToken);

    if (namesake is null)
      return null;

    if (namesake.IsHidden)
      return Refusal.TablePlan.TableNameTakenByHiddenTable(festivalId, name, namesake.AreaName);

    return Refusal.TablePlan.TableNameTaken(festivalId, name, namesake.AreaName);
  }

  private sealed record TableNamesake(string AreaName, bool IsHidden);
}
