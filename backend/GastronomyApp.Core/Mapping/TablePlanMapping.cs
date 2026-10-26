using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.TablePlan;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using Mapster;

namespace GastronomyApp.Core.Mapping;

public sealed class TablePlanMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<FestivalStationShape, AdminStationShapeView>().Map(view => view.StationId, shape => shape.FestivalStation.StationId).Map(view => view.StationName, shape => shape.FestivalStation.Station.Name).Map(view => view.Geometry, shape => shape);

    config.NewConfig<Table, AdminTableView>().Map(view => view.TableId, table => table.Id).Map(view => view.Geometry, table => table).Map(view => view.HasItemsToHandOutOrSettleAtTheRunningFestival, table => false);

    config.NewConfig<Area, AdminAreaView>()
          .Map(view => view.AreaId, area => area.Id)
          .Map(view => view.HasItemsToHandOutOrSettleAtTheRunningFestival, area => false)
          .Map(view => view.Tables, area => area.Tables.OrderBy(table => table.Name))
          .Map(view => view.StationShapes, area => area.StationShapes.OrderBy(shape => shape.FestivalStation.Station.SortOrder).ThenBy(shape => shape.FestivalStation.Station.Name));

    config.NewConfig<FestivalStationShape, TablePlanStationShapeView>().Map(view => view.StationName, shape => shape.FestivalStation.Station.Name).Map(view => view.Geometry, shape => shape);

    config.NewConfig<OrderItem, OrderItemFlags>().Map(flags => flags.IsHandedOut, OrderItemStates.HandedOutPredicate).Map(flags => flags.IsSettled, item => item.SettledAtUtc != null);

    config.NewConfig<Table, ShownTable>().Map(shown => shown.TableId, table => table.Id).Map(shown => shown.Geometry, table => table).Map(shown => shown.ItemFlagsPerOrder, table => table.Orders.Select(order => order.StationOrders.SelectMany(stationOrder => stationOrder.Items)));

    config.NewConfig<Area, ShownArea>()
          .Map(shown => shown.AreaId, area => area.Id)
          .Map(shown => shown.Tables, area => area.Tables.Where(table => !table.IsHidden).OrderBy(table => table.Name))
          .Map(shown => shown.StationShapes, area => area.StationShapes.OrderBy(shape => shape.FestivalStation.Station.SortOrder).ThenBy(shape => shape.FestivalStation.Station.Name));

    config.NewConfig<ShownTable, TablePlanTableView>().Map(view => view.HandOutProgress, shown => ShareState.None).Map(view => view.SettlementProgress, shown => ShareState.None);

    config.NewConfig<Area, SavedAreaView>().Map(view => view.AreaId, area => area.Id);

    config.NewConfig<Table, SavedTableView>().Map(view => view.TableId, table => table.Id);
  }
}
