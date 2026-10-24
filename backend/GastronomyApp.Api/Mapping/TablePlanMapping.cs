using GastronomyApp.Contracts.TablePlan;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class TablePlanMapping : IMappingRegistration
{
  private readonly TableService _tableService;

  public TablePlanMapping(TableService tableService)
  {
    _tableService = tableService;
  }

  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<ShownTable, TablePlanTableView>()
          .Map(view => view.TableId, shown => shown.Table.Id)
          .Map(view => view.Name, shown => shown.Table.Name)
          .Map(view => view.Geometry, shown => shown.Table)
          .Map(view => view.HandOutProgress, shown => _tableService.HandOutProgressOf(shown))
          .Map(view => view.SettlementProgress, shown => _tableService.SettlementProgressOf(shown));

    config.NewConfig<NamedStationShape, TablePlanStationShapeView>().Map(view => view.Geometry, named => named.Shape);

    config.NewConfig<ShownArea, TablePlanAreaView>().Map(view => view.AreaId, shown => shown.Area.Id).Map(view => view.Name, shown => shown.Area.Name);
  }
}
