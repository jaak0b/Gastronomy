using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Results;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminTablePlanMapping : IMappingRegistration
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<ListedTable, AdminTableView>().Map(view => view.TableId, listed => listed.Table.Id).Map(view => view.Name, listed => listed.Table.Name).Map(view => view.Geometry, listed => listed.Table).Map(view => view.IsHidden, listed => listed.Table.IsHidden);

    config.NewConfig<ShownStationShape, AdminStationShapeView>().Map(view => view.Geometry, shown => shown.Shape);

    config.NewConfig<ListedArea, AdminAreaView>().Map(view => view.AreaId, listed => listed.Area.Id).Map(view => view.Name, listed => listed.Area.Name).Map(view => view.SortOrder, listed => listed.Area.SortOrder).Map(view => view.IsHidden, listed => listed.Area.IsHidden);
  }
}
