using ErrorOr;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Contracts.TablePlan;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class TablePlanService
{
  private readonly IGastronomyDatabase _database;
  private readonly TypeAdapterConfig _mapping;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly TableProgress _tableProgress;

  public TablePlanService(IGastronomyDatabase database, TypeAdapterConfig mapping, RunningFestivalLookup runningFestival, TableProgress tableProgress)
  {
    _database = database;
    _mapping = mapping;
    _runningFestival = runningFestival;
    _tableProgress = tableProgress;
  }

  public async Task<ErrorOr<AdminTablePlanView>> ListAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var festival = await _database.Festivals.AsNoTracking().FirstOrDefaultAsync(row => row.Id == festivalId, cancellationToken);

    if (festival is null)
      return Refusal.TablePlan.FestivalNotFound(festivalId);

    var areas = await _database.Areas.AsNoTracking().Where(area => area.FestivalId == festivalId).OrderBy(area => area.SortOrder).ThenBy(area => area.Name).ProjectToType<AdminAreaView>(_mapping).ToListAsync(cancellationToken);
    IReadOnlyList<TableHoldingItems> tablesHoldingItems = [];

    if (_runningFestival.IsRunning(festival))
      tablesHoldingItems = await FindTablesHoldingItemsAsync(festivalId, cancellationToken);

    var tableIdsHoldingItems = tablesHoldingItems.Select(table => table.TableId).ToHashSet();
    var areaIdsHoldingItems = tablesHoldingItems.Select(table => table.AreaId).ToHashSet();

    return new AdminTablePlanView(areas.Select(area => area with
                                                       {
                                                         HasItemsToHandOutOrSettleAtTheRunningFestival = areaIdsHoldingItems.Contains(area.AreaId),
                                                         Tables = area.Tables.Select(table => table with { HasItemsToHandOutOrSettleAtTheRunningFestival = tableIdsHoldingItems.Contains(table.TableId) }).ToList()
                                                       })
                                       .ToList());
  }

  public async Task<TablePlanView> ReadRunningFestivalAsync(CancellationToken cancellationToken)
  {
    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return new([]);

    var areas = await _database.Areas.AsNoTracking().Where(area => area.FestivalId == festival.Id && !area.IsHidden).OrderBy(area => area.SortOrder).ThenBy(area => area.Name).ProjectToType<ShownArea>(_mapping).ToListAsync(cancellationToken);

    return new(areas.Select(ViewOf).ToList());
  }

  private async Task<IReadOnlyList<TableHoldingItems>> FindTablesHoldingItemsAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _database.OrderItems.AsNoTracking().WhereNotHandedOutOrNotSettled().WhereFestivalIs(festivalId).Select(item => new TableHoldingItems(item.StationOrder.Order.TableId, item.StationOrder.Order.Table.AreaId)).Distinct().ToListAsync(cancellationToken);
  }

  private TablePlanAreaView ViewOf(ShownArea shown)
  {
    var view = shown.Adapt<TablePlanAreaView>(_mapping);

    return view with
           {
             Tables = view.Tables.Zip(shown.Tables,
                                      (tableView, shownTable) => tableView with
                                                                 {
                                                                   HandOutProgress = _tableProgress.HandOutProgressOf(shownTable),
                                                                   SettlementProgress = _tableProgress.SettlementProgressOf(shownTable)
                                                                 })
                          .ToList()
           };
  }

  private sealed record TableHoldingItems(Guid TableId, Guid AreaId);
}
