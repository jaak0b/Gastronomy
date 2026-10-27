using GastronomyApp.Contracts.Stations;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using Mapster;

namespace GastronomyApp.Core.Mapping;

public sealed class StationQueueMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<OrderItem, StationQueueItemView>().Map(view => view.OrderItemId, item => item.Id).Map(view => view.State, item => item.State()).Map(view => view.StateSetAtUtc, item => item.StateSetAtUtc());

    config.NewConfig<StationOrder, StationOrderQueueView>()
          .Map(view => view.StationOrderId, stationOrder => stationOrder.Id)
          .Map(view => view.GlobalOrderNumber, stationOrder => stationOrder.Order.GlobalOrderNumber)
          .Map(view => view.TableName, stationOrder => stationOrder.Order.Table.Name)
          .Map(view => view.StaffMemberName, stationOrder => stationOrder.Order.StaffMember.Name)
          .Map(view => view.CreatedAtUtc, stationOrder => stationOrder.Order.CreatedAtUtc)
          .Map(view => view.ItemCount, stationOrder => stationOrder.Items.Count(item => !item.IsCancelled()))
          .Map(view => view.FulfilledItemCount, stationOrder => stationOrder.Items.Count(item => item.IsHandedOut()))
          .Map(view => view.Items, stationOrder => stationOrder.Items.OrderBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Note, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToList());

    config.NewConfig<Station, StationSummaryView>();
  }
}
