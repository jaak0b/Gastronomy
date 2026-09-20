using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Services;

public sealed class OpenItemsService
{
  private readonly IClock _clock;
  private readonly TimeSpan _givenAwayLookback = TimeSpan.FromHours(24);
  private readonly IOpenItemRepository _repository;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly OrderItemSettlementService _settlementService;

  public OpenItemsService(IOpenItemRepository repository, RunningFestivalLookup runningFestival, OrderItemSettlementService settlementService, IClock clock)
  {
    _repository = repository;
    _runningFestival = runningFestival;
    _settlementService = settlementService;
    _clock = clock;
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
    IReadOnlyList<OrderItem> givenAwayItems = await _repository.FindGivenAwayAtFestivalSinceAsync(festival.Id, _clock.UtcNow - _givenAwayLookback, cancellationToken);

    List<OrderItem> allItems = openItems.Concat(givenAwayItems).ToList();
    IReadOnlyDictionary<Guid, OrderItemOwner> owners = await _repository.FindOwnersAsync(allItems.Select(item => item.Id).Distinct().ToList(), cancellationToken);

    Dictionary<string, List<OrderItem>> openByTable = GroupByTable(openItems, owners);
    Dictionary<string, List<OrderItem>> givenAwayByTable = GroupByTable(givenAwayItems, owners);

    return new()
           {
             Tables = openByTable.Keys.Concat(givenAwayByTable.Keys)
                                 .Distinct(StringComparer.Ordinal)
                                 .OrderBy(tableName => tableName, StringComparer.Ordinal)
                                 .Select(tableName => BuildOpenTable(tableName, ItemsAtTable(openByTable, tableName), ItemsAtTable(givenAwayByTable, tableName), owners))
                                 .ToList(),
             OrderItemIdsWithoutAnOrder = allItems.Where(item => !owners.ContainsKey(item.Id)).Select(item => item.Id).Distinct().ToList()
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

  private OpenTable BuildOpenTable(string tableName, IReadOnlyCollection<OrderItem> openItems, IReadOnlyCollection<OrderItem> givenAwayItems, IReadOnlyDictionary<Guid, OrderItemOwner> owners)
  {
    return new()
           {
             TableName = tableName,
             OpenAmountCents = _settlementService.SumOpenAmountCents(openItems),
             GivenAwayAmountCents = _settlementService.SumWaivedAmountCents(givenAwayItems),
             Items = BuildOpenItems(openItems, owners),
             GivenAwayItems = BuildGivenAwayItems(givenAwayItems, owners)
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

  private IReadOnlyList<GivenAwayOrderItem> BuildGivenAwayItems(IEnumerable<OrderItem> items, IReadOnlyDictionary<Guid, OrderItemOwner> owners)
  {
    return items.Select(item => new GivenAwayOrderItem
                                {
                                  OrderItemId = item.Id,
                                  OrderId = owners[item.Id].OrderId,
                                  GlobalOrderNumber = owners[item.Id].GlobalOrderNumber,
                                  ItemName = item.ItemName,
                                  WaivedAmountCents = _settlementService.CalculateWaivedAmountCents(item),
                                  PaymentNotice = item.PaymentNotice,
                                  SettledAtUtc = item.SettledAtUtc ?? owners[item.Id].OrderedAtUtc
                                })
                .OrderBy(givenAwayItem => givenAwayItem.GlobalOrderNumber)
                .ThenBy(givenAwayItem => givenAwayItem.ItemName, StringComparer.Ordinal)
                .ToList();
  }
}
