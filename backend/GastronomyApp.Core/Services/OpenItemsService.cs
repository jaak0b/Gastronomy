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

    IReadOnlyDictionary<Guid, OrderItemOwner> owners = await _repository.FindOwnersAsync(openItems.Select(item => item.Id).Distinct().ToList(), cancellationToken);

    Dictionary<string, List<OrderItem>> openByTable = GroupByTable(openItems, owners);

    return new()
           {
             Tables = openByTable.Keys.OrderBy(tableName => tableName, StringComparer.Ordinal)
                                 .Select(tableName => BuildOpenTable(tableName, ItemsAtTable(openByTable, tableName), owners))
                                 .ToList(),
             OrderItemIdsWithoutAnOrder = openItems.Where(item => !owners.ContainsKey(item.Id)).Select(item => item.Id).Distinct().ToList()
           };
  }

  public async Task<IReadOnlyList<string>> ReadTableNamesAsync(CancellationToken cancellationToken)
  {
    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return [];

    return await _repository.FindTableNamesAtFestivalAsync(festival.Id, cancellationToken);
  }

  private IReadOnlyCollection<OrderItem> ItemsAtTable(Dictionary<string, List<OrderItem>> byTable, string tableName)
  {
    if (byTable.TryGetValue(tableName, out List<OrderItem>? items))
      return items;

    return [];
  }

  private OpenTable BuildOpenTable(string tableName, IReadOnlyCollection<OrderItem> openItems, IReadOnlyDictionary<Guid, OrderItemOwner> owners)
  {
    return new()
           {
             TableName = tableName,
             OpenAmountCents = _settlementService.SumOpenAmountCents(openItems),
             Items = BuildOpenItems(openItems, owners)
           };
  }

  private Dictionary<string, List<OrderItem>> GroupByTable(IEnumerable<OrderItem> items, IReadOnlyDictionary<Guid, OrderItemOwner> owners)
  {
    return items.Where(item => owners.ContainsKey(item.Id)).GroupBy(item => owners[item.Id].TableName, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
  }

  private IReadOnlyList<OpenOrderItem> BuildOpenItems(IEnumerable<OrderItem> items, IReadOnlyDictionary<Guid, OrderItemOwner> owners)
  {
    return items.Select(item => new OpenOrderItem
                                {
                                  OrderItemId = item.Id,
                                  OrderId = owners[item.Id].OrderId,
                                  GlobalOrderNumber = owners[item.Id].GlobalOrderNumber,
                                  ItemName = item.ItemName,
                                  Note = item.Note,
                                  UnitPriceCents = item.UnitPriceCents,
                                  OrderedAtUtc = owners[item.Id].OrderedAtUtc
                                })
                .OrderBy(openItem => openItem.GlobalOrderNumber)
                .ThenBy(openItem => openItem.ItemName, StringComparer.Ordinal)
                .ToList();
  }
}
