using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Requests;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class OrderMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<OrderSettlementLineRequest, OrderSettlementLineTerms>();

    config.NewConfig<OrderItemRequest, OrderAcceptanceItemRequest>();

    config.NewConfig<OrderDeliveryModeRequest, StationDeliveryModeRequest>();

    config.NewConfig<SettleLineRequest, SettlementLine>();

    config.NewConfig<PlacedStationOrder, StationOrderView>().Map(view => view.ItemIds, stationOrder => stationOrder.Items.Select(item => item.OrderItemId).ToList());

    config.NewConfig<PlacedOrderReport, PlacedOrderView>()
          .Map(view => view.OrderId, report => report.Order.OrderId)
          .Map(view => view.GlobalOrderNumber, report => report.Order.GlobalOrderNumber)
          .Map(view => view.CreatedAtUtc, report => report.Order.CreatedAtUtc)
          .Map(view => view.StationOrders, report => report.Order.StationOrders);
  }
}
