using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Core.Services;

public sealed class OpenItemsService
{
  private readonly ILogger<OpenItemsService> _logger;
  private readonly IOpenItemRepository _repository;
  private readonly RunningFestivalLookup _runningFestival;

  public OpenItemsService(IOpenItemRepository repository, RunningFestivalLookup runningFestival, ILogger<OpenItemsService> logger)
  {
    _repository = repository;
    _runningFestival = runningFestival;
    _logger = logger;
  }

  public async Task<IReadOnlyList<OrderItem>> ReadOpenItemsAsync(CancellationToken cancellationToken)
  {
    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return [];

    return await _repository.FindOpenAtFestivalAsync(festival.Id, cancellationToken);
  }

  public IReadOnlyList<IGrouping<Guid, OrderItem>> GroupItemsWithAKnownOrderByTable(IEnumerable<OrderItem> openItems)
  {
    ArgumentNullException.ThrowIfNull(openItems);

    return openItems.Where(item => item.StationOrder?.Order is not null)
                    .OrderBy(item => item.StationOrder!.Order!.GlobalOrderNumber)
                    .ThenBy(item => item.ItemName, StringComparer.Ordinal)
                    .GroupBy(item => item.StationOrder!.Order!.TableId)
                    .OrderBy(table => table.First().StationOrder.Order.Table.Name, StringComparer.Ordinal)
                    .ThenBy(table => table.Key)
                    .ToList();
  }

  public IReadOnlyList<Guid> ItemIdsWithoutAnOrder(IEnumerable<OrderItem> openItems)
  {
    ArgumentNullException.ThrowIfNull(openItems);

    List<Guid> itemIdsWithoutAnOrder = openItems.Where(item => item.StationOrder?.Order is null).Select(item => item.Id).Distinct().ToList();

    if (itemIdsWithoutAnOrder.Count > 0)
      _logger.LogError("{ItemCount} order items cannot be traced back to an order and are therefore missing from the open items list. Order item ids: {OrderItemIds}.", itemIdsWithoutAnOrder.Count, itemIdsWithoutAnOrder);

    return itemIdsWithoutAnOrder;
  }

  public async Task<ErrorOr<Table>> ReadTableWithOrdersAsync(Guid tableId, CancellationToken cancellationToken)
  {
    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return Refusal.Settlement.NoRunningFestival();

    var table = await _repository.FindTableWithOrdersAsync(festival.Id, tableId, cancellationToken);

    if (table is null)
      return Refusal.Settlement.TableNotAtTheRunningFestival(tableId);

    return table;
  }
}
