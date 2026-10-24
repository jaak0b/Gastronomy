using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminTablePlanMapping : IMappingRegistration
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<Table, AdminTableView>().Map(view => view.TableId, table => table.Id).Map(view => view.Geometry, table => table);

    config.NewConfig<FestivalStationShape, AdminStationShapeView>().Map(view => view.StationId, shape => shape.FestivalStation.StationId).Map(view => view.StationName, shape => shape.FestivalStation.Station.Name).Map(view => view.Geometry, shape => shape);

    config.NewConfig<Area, AdminAreaView>().Map(view => view.AreaId, area => area.Id);
  }
}
