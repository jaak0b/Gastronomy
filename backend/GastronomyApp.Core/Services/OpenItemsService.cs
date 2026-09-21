using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Services;

public sealed class OpenItemsService
{
  private readonly IOpenItemRepository _repository;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly OrderItemSettlementService _settlementService;

  public OpenItemsService(IOpenItemRepository repository, RunningFestivalLookup runningFestival, OrderItemSettlementService settlementService)
  {
    _repository = repository;
    _runningFestival = runningFestival;
    _settlementService = settlementService;
  }

  public async Task<OpenItemsReport> ReadAsync(CancellationToken cancellationToken)
  {
    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
    {
      return new()
             {
               Tables = [],
               OrderItemIdsWithoutAnOrder = []
             };
    }

    IReadOnlyList<OrderItem> openItems = await _repository.FindOpenAtFestivalAsync(festival.Id, cancellationToken);

    Dictionary<string, List<OrderItem>> openByTable = GroupByTable(openItems);

    return new()
           {
             Tables = openByTable.Keys.OrderBy(tableName => tableName, StringComparer.Ordinal).Select(tableName => BuildOpenTable(tableName, ItemsAtTable(openByTable, tableName))).ToList(),
             OrderItemIdsWithoutAnOrder = openItems.Where(item => OrderOf(item) is null).Select(item => item.Id).Distinct().ToList()
           };
  }

  public async Task<IReadOnlyList<string>> ReadTableNamesAsync(CancellationToken cancellationToken)
  {
    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return [];

    return await _repository.FindTableNamesAtFestivalAsync(festival.Id, cancellationToken);
  }

  public async Task<TableOrderReport> ReadTableAsync(string tableName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(tableName);

    if (string.IsNullOrWhiteSpace(tableName))
      return EmptyTableReport(tableName);

    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return EmptyTableReport(tableName);

    IReadOnlyList<TableOrderRecord> orders = await _repository.FindTableOrdersAsync(festival.Id, tableName, cancellationToken);

    return new()
           {
             TableName = tableName,
             OpenAmountCents = _settlementService.SumOpenAmountCents(orders.SelectMany(order => order.Items)),
             Orders = orders
           };
  }

  private Order? OrderOf(OrderItem item)
  {
    return item.StationOrder?.Order;
  }

  private IReadOnlyCollection<OrderItem> ItemsAtTable(Dictionary<string, List<OrderItem>> byTable, string tableName)
  {
    if (byTable.TryGetValue(tableName, out List<OrderItem>? items))
      return items;

    return [];
  }

  private TableOrderReport EmptyTableReport(string tableName)
  {
    return new()
           {
             TableName = tableName,
             OpenAmountCents = 0,
             Orders = []
           };
  }

  private OpenTable BuildOpenTable(string tableName, IReadOnlyCollection<OrderItem> openItems)
  {
    return new()
           {
             TableName = tableName,
             OpenAmountCents = _settlementService.SumOpenAmountCents(openItems),
             Items = BuildOpenItems(openItems)
           };
  }

  private Dictionary<string, List<OrderItem>> GroupByTable(IEnumerable<OrderItem> items)
  {
    return items.Where(item => OrderOf(item) is not null).GroupBy(item => OrderOf(item)!.TableName, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
  }

  private IReadOnlyList<OpenOrderItem> BuildOpenItems(IEnumerable<OrderItem> items)
  {
    return items.Select(item => new OpenOrderItem
                                {
                                  OrderItemId = item.Id,
                                  OrderId = OrderOf(item)!.Id,
                                  GlobalOrderNumber = OrderOf(item)!.GlobalOrderNumber,
                                  ItemName = item.ItemName,
                                  Note = item.Note,
                                  UnitPriceCents = item.UnitPriceCents,
                                  OrderedAtUtc = OrderOf(item)!.CreatedAtUtc
                                })
                .OrderBy(openItem => openItem.GlobalOrderNumber)
                .ThenBy(openItem => openItem.ItemName, StringComparer.Ordinal)
                .ToList();
  }
}
