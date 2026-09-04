using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure;

namespace GastronomyApp.Api.Endpoints;

public sealed class OrderQueryHandler
{
  private readonly GastronomyAppDbContext _dbContext;

  private readonly OrderReader _orderReader;
  private readonly StationPrinterStatusLookup _statusLookup;

  public OrderQueryHandler(GastronomyAppDbContext dbContext,
                           OrderReader orderReader,
                           StationPrinterStatusLookup statusLookup)
  {
    _dbContext = dbContext;
    _orderReader = orderReader;
    _statusLookup = statusLookup;
  }

  public async Task<OrderListEntryView> DescribeListEntryAsync(LoadedOrder loaded,
                                                               CancellationToken cancellationToken)
  {
    HashSet<Guid> stationIds = [.. loaded.StationOrders.Select(stationOrder => stationOrder.StationId)];

    Dictionary<Guid, PrinterStatus> printerStatuses =
      await _statusLookup.ByStationAsync(_dbContext, stationIds, cancellationToken);

    List<OrderListStationOrderView> stationOrders =
    [
      .. loaded.StationOrders.Select(stationOrder => new OrderListStationOrderView(stationOrder.Id,
                                                                                   loaded.StationNames.TryGetValue(stationOrder.StationId, out var name) ? name : string.Empty,
                                                                                   stationOrder.StationOrderNumber,
                                                                                   _orderReader.StatusOfStationOrder(loaded, stationOrder.Id).ToString(),
                                                                                   loaded.LatestPrintJobByStationOrderId.TryGetValue(stationOrder.Id, out var job)
                                                                                     ? job.FailureReason?.ToString()
                                                                                     : null,
                                                                                   !printerStatuses.TryGetValue(stationOrder.StationId, out var status)
                                                                                   || !status.IsPaperEnd))
    ];

    return new(loaded.Order.Id,
               loaded.Order.GlobalOrderNumber,
               loaded.Order.TableName,
               _orderReader.TotalCentsOf(loaded),
               _orderReader.StatusOf(loaded).ToString(),
               loaded.Order.CreatedAtUtc,
               stationOrders);
  }
}
