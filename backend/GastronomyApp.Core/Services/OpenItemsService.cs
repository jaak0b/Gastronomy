using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Services;

public sealed class OpenItemsService
{
  private readonly IOpenItemRepository _repository;
  private readonly RunningFestivalLookup _runningFestival;

  public OpenItemsService(IOpenItemRepository repository, RunningFestivalLookup runningFestival)
  {
    _repository = repository;
    _runningFestival = runningFestival;
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

    return openItems.Where(item => OrderOf(item) is not null).OrderBy(item => OrderOf(item)!.GlobalOrderNumber).ThenBy(item => item.ItemName, StringComparer.Ordinal).GroupBy(item => OrderOf(item)!.TableName, StringComparer.Ordinal).OrderBy(table => table.Key, StringComparer.Ordinal).ToList();
  }

  public IReadOnlyList<Guid> ItemIdsWithoutAnOrder(IEnumerable<OrderItem> openItems)
  {
    ArgumentNullException.ThrowIfNull(openItems);

    return openItems.Where(item => OrderOf(item) is null).Select(item => item.Id).Distinct().ToList();
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

  private Order? OrderOf(OrderItem item)
  {
    return item.StationOrder?.Order;
  }
}
