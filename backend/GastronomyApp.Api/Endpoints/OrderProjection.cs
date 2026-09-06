using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public sealed record LoadedOrder(
  Order Order,
  IReadOnlyList<StationOrder> StationOrders,
  IReadOnlyList<OrderItem> Items,
  IReadOnlyDictionary<Guid, string> StationNames);

public sealed class OrderReader
{
  private readonly OrderStatusCalculator _statusCalculator;

  public OrderReader(OrderStatusCalculator statusCalculator)
  {
    _statusCalculator = statusCalculator;
  }

  public async Task<LoadedOrder?> LoadAsync(GastronomyAppDbContext context,
                                            Guid orderId,
                                            CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(context);

    var order = await context.Orders
                             .AsNoTracking()
                             .FirstOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

    return order is null ? null : await LoadForAsync(context, order, cancellationToken);
  }

  public OrderStatus StatusOf(LoadedOrder loaded)
  {
    ArgumentNullException.ThrowIfNull(loaded);

    return _statusCalculator.Calculate([.. loaded.Items.Select(item => item.ProductionStatus)]);
  }

  public OrderStatus StatusOfStationOrder(LoadedOrder loaded, Guid stationOrderId)
  {
    ArgumentNullException.ThrowIfNull(loaded);

    return _statusCalculator.Calculate([
                                        .. loaded.Items
                                                 .Where(item => item.StationOrderId == stationOrderId)
                                                 .Select(item => item.ProductionStatus)
                                      ]);
  }

  public int TotalCentsOf(LoadedOrder loaded)
  {
    ArgumentNullException.ThrowIfNull(loaded);

    return loaded.Items.Sum(item => item.UnitPriceCents);
  }

  public PlacedOrderView Describe(LoadedOrder loaded)
  {
    ArgumentNullException.ThrowIfNull(loaded);

    return new(loaded.Order.Id,
               loaded.Order.GlobalOrderNumber,
               StatusOf(loaded),
               TotalCentsOf(loaded),
               loaded.Order.CreatedAtUtc,
               DescribeStationOrders(loaded));
  }

  public OrderListEntryView DescribeListEntry(LoadedOrder loaded)
  {
    ArgumentNullException.ThrowIfNull(loaded);

    return new(loaded.Order.Id,
               loaded.Order.GlobalOrderNumber,
               loaded.Order.TableName,
               TotalCentsOf(loaded),
               StatusOf(loaded),
               loaded.Order.CreatedAtUtc,
               [
                 .. loaded.StationOrders.Select(stationOrder => new OrderListStationOrderView(stationOrder.Id,
                                                                                               NameOf(loaded, stationOrder.StationId),
                                                                                               stationOrder.StationOrderNumber,
                                                                                               stationOrder.DeliveryMode,
                                                                                               StatusOfStationOrder(loaded, stationOrder.Id)))
               ]);
  }

  public IReadOnlyList<StationOrderView> DescribeStationOrders(LoadedOrder loaded)
  {
    ArgumentNullException.ThrowIfNull(loaded);

    return
    [
      .. loaded.StationOrders.Select(stationOrder => new StationOrderView(stationOrder.Id,
                                                                          stationOrder.StationId,
                                                                          NameOf(loaded, stationOrder.StationId),
                                                                          stationOrder.StationOrderNumber,
                                                                          stationOrder.DeliveryMode,
                                                                          [
                                                                            .. loaded.Items
                                                                                     .Where(item => item.StationOrderId == stationOrder.Id)
                                                                                     .Select(item => item.Id)
                                                                          ]))
    ];
  }

  private string NameOf(LoadedOrder loaded, Guid stationId)
  {
    return loaded.StationNames.TryGetValue(stationId, out var name) ? name : string.Empty;
  }

  private async Task<LoadedOrder> LoadForAsync(GastronomyAppDbContext context,
                                               Order order,
                                               CancellationToken cancellationToken)
  {
    List<StationOrder> stationOrders = await context.StationOrders
                                                    .AsNoTracking()
                                                    .Where(stationOrder => stationOrder.OrderId == order.Id)
                                                    .OrderBy(stationOrder => stationOrder.StationOrderNumber)
                                                    .ThenBy(stationOrder => stationOrder.Id)
                                                    .ToListAsync(cancellationToken);

    List<Guid> stationOrderIds = [.. stationOrders.Select(stationOrder => stationOrder.Id)];

    List<OrderItem> items = await context.OrderItems
                                         .AsNoTracking()
                                         .Where(item => stationOrderIds.Contains(item.StationOrderId))
                                         .OrderBy(item => item.Id)
                                         .ToListAsync(cancellationToken);

    HashSet<Guid> stationIds = [.. stationOrders.Select(stationOrder => stationOrder.StationId)];

    Dictionary<Guid, string> stationNames = await context.Stations
                                                         .AsNoTracking()
                                                         .Where(station => stationIds.Contains(station.Id))
                                                         .ToDictionaryAsync(station => station.Id, station => station.Name, cancellationToken);

    return new(order, stationOrders, items, stationNames);
  }
}
