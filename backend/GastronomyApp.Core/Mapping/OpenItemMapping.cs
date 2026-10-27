using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Results;
using Mapster;

namespace GastronomyApp.Core.Mapping;

public sealed class OpenItemMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<OrderItem, TableOrderRecordItemView>()
          .Map(view => view.OrderItemId, item => item.Id)
          .Map(view => view.OrderId, item => item.StationOrder.Order.Id)
          .Map(view => view.GlobalOrderNumber, item => item.StationOrder.Order.GlobalOrderNumber)
          .Map(view => view.OrderedAtUtc, item => item.StationOrder.Order.CreatedAtUtc)
          .Map(view => view.State, item => item.State())
          .Map(view => view.StateSetAtUtc, item => item.StateSetAtUtc());

    config.NewConfig<Order, TableOrderRecordView>()
          .Map(view => view.OrderId, order => order.Id)
          .Map(view => view.StaffMemberName, order => order.StaffMember.Name)
          .Map(view => view.Items, order => order.StationOrders.SelectMany(stationOrder => stationOrder.Items).OrderBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Note, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToList());

    config.NewConfig<Table, TableOrderReportView>()
          .Map(view => view.TableId, table => table.Id)
          .Map(view => view.TableName, table => table.Name)
          .Map(view => view.OpenAmountCents, table => table.Orders.SelectMany(order => order.StationOrders).SelectMany(stationOrder => stationOrder.Items).Where(item => item.SettledAtUtc == null && !item.IsCancelled()).Sum(item => item.UnitPriceCents));

    config.NewConfig<SettlementResult, SettlementView>()
          .Map(view => view.SettledOrderItemIds, settlement => settlement.NewlySettled.Select(item => item.Id).ToList())
          .Map(view => view.ReappliedOrderItemIds, settlement => settlement.Reapplied.Select(item => item.Id).ToList())
          .Map(view => view.AlreadySettledByOthersOrderItemIds, settlement => settlement.AlreadySettledByOthers.Select(item => item.Id).ToList());
  }
}
