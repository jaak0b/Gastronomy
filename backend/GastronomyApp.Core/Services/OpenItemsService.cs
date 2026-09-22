using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
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

  public IReadOnlyList<IGrouping<string, OrderItem>> GroupItemsWithAKnownOrderByTableName(IEnumerable<OrderItem> openItems)
  {
    ArgumentNullException.ThrowIfNull(openItems);

    return openItems.Where(item => item.StationOrder?.Order is not null)
                    .OrderBy(item => item.StationOrder!.Order!.GlobalOrderNumber)
                    .ThenBy(item => item.ItemName, StringComparer.Ordinal)
                    .GroupBy(item => item.StationOrder!.Order!.TableName, StringComparer.Ordinal)
                    .OrderBy(table => table.Key, StringComparer.Ordinal)
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

  public async Task<IReadOnlyList<string>> ReadTableNamesAsync(CancellationToken cancellationToken)
  {
    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return [];

    return await _repository.FindTableNamesAtFestivalAsync(festival.Id, cancellationToken);
  }

  public async Task<IReadOnlyList<Order>> ReadTableOrdersAsync(string tableName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(tableName);

    if (string.IsNullOrWhiteSpace(tableName))
      return [];

    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return [];

    return await _repository.FindTableOrdersAsync(festival.Id, tableName, cancellationToken);
  }
}
