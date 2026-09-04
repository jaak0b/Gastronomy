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
  IReadOnlyDictionary<Guid, string> StationNames,
  IReadOnlyDictionary<Guid, PrintJob> LatestPrintJobByStationOrderId);

public sealed class OrderReader
{
  private readonly OrderStatusCalculator _statusCalculator = new();

  public async Task<LoadedOrder?> LoadAsync(GastronomyAppDbContext context,
                                            Guid orderId,
                                            CancellationToken cancellationToken)
  {
    var order = await context.Orders
                             .AsNoTracking()
                             .FirstOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

    return order is null ? null : await LoadForAsync(context, order, cancellationToken);
  }

  public async Task<LoadedOrder?> LoadByClientOrderIdAsync(GastronomyAppDbContext context,
                                                           Guid clientOrderId,
                                                           CancellationToken cancellationToken)
  {
    var order = await context.Orders
                             .AsNoTracking()
                             .FirstOrDefaultAsync(candidate => candidate.ClientOrderId == clientOrderId, cancellationToken);

    return order is null ? null : await LoadForAsync(context, order, cancellationToken);
  }

  public OrderStatus StatusOf(LoadedOrder loaded)
  {
    return _statusCalculator.Calculate([
                                        .. loaded.StationOrders.Select(stationOrder =>
                                                                         loaded.LatestPrintJobByStationOrderId.TryGetValue(stationOrder.Id, out var job)
                                                                           ? job.Status
                                                                           : PrintJobStatus.Queued)
                                      ]);
  }

  public int TotalCentsOf(LoadedOrder loaded)
  {
    return loaded.Items.Sum(item => item.UnitPriceCents);
  }

  public PlacedOrderView Describe(LoadedOrder loaded)
  {
    return new(loaded.Order.Id,
               loaded.Order.GlobalOrderNumber,
               StatusOf(loaded).ToString(),
               TotalCentsOf(loaded),
               loaded.Order.CreatedAtUtc,
               DescribeStationOrders(loaded));
  }

  public IReadOnlyList<StationOrderView> DescribeStationOrders(LoadedOrder loaded)
  {
    return
    [
      .. loaded.StationOrders.Select(stationOrder => new StationOrderView(stationOrder.Id,
                                                                          stationOrder.StationId,
                                                                          loaded.StationNames.TryGetValue(stationOrder.StationId, out var name) ? name : string.Empty,
                                                                          stationOrder.StationOrderNumber,
                                                                          StatusOfStationOrder(loaded, stationOrder.Id).ToString(),
                                                                          [
                                                                            .. loaded.Items
                                                                                     .Where(item => item.StationOrderId == stationOrder.Id)
                                                                                     .Select(item => item.Id)
                                                                          ]))
    ];
  }

  public PrintJobStatus StatusOfStationOrder(LoadedOrder loaded, Guid stationOrderId)
  {
    return loaded.LatestPrintJobByStationOrderId.TryGetValue(stationOrderId, out var job)
             ? job.Status
             : PrintJobStatus.Queued;
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

    List<PrintJob> printJobs = await context.PrintJobs
                                            .AsNoTracking()
                                            .Where(job => stationOrderIds.Contains(job.StationOrderId))
                                            .ToListAsync(cancellationToken);

    Dictionary<Guid, PrintJob> latestByStationOrderId = printJobs
                                                       .GroupBy(job => job.StationOrderId)
                                                       .ToDictionary(group => group.Key,
                                                                     group => group.OrderByDescending(job => job.CopyNumber).First());

    HashSet<Guid> stationIds = [.. stationOrders.Select(stationOrder => stationOrder.StationId)];

    Dictionary<Guid, string> stationNames = await context.Stations
                                                         .AsNoTracking()
                                                         .Where(station => stationIds.Contains(station.Id))
                                                         .ToDictionaryAsync(station => station.Id, station => station.Name, cancellationToken);

    return new(order, stationOrders, items, stationNames, latestByStationOrderId);
  }
}
