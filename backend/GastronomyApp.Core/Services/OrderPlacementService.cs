using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Refusals;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class OrderPlacementService
{
  private readonly IGastronomyDatabase _database;
  private readonly OrderItemLifecycle _itemLifecycle;
  private readonly TypeAdapterConfig _mapping;
  private readonly SequenceNumberAllocator _numberAllocator;
  private readonly OrderService _orderService;
  private readonly OrderRoutingResolver _routingResolver;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly StockSoldOutMarker _soldOutMarker;
  private readonly TimeProvider _timeProvider;

  public OrderPlacementService(IGastronomyDatabase database,
                               TypeAdapterConfig mapping,
                               RunningFestivalLookup runningFestival,
                               SequenceNumberAllocator numberAllocator,
                               OrderService orderService,
                               OrderRoutingResolver routingResolver,
                               StockSoldOutMarker soldOutMarker,
                               OrderItemLifecycle itemLifecycle,
                               TimeProvider timeProvider)
  {
    _database = database;
    _mapping = mapping;
    _runningFestival = runningFestival;
    _numberAllocator = numberAllocator;
    _orderService = orderService;
    _routingResolver = routingResolver;
    _soldOutMarker = soldOutMarker;
    _itemLifecycle = itemLifecycle;
    _timeProvider = timeProvider;
  }

  public async Task<ErrorOr<PlacedOrderView>> PlaceAsync(PlaceOrderRequest request, Guid staffMemberId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var existingOrderId = await _database.Orders.AsNoTracking().Where(order => order.ClientOrderId == request.ClientOrderId).Select(order => (Guid?)order.Id).FirstOrDefaultAsync(cancellationToken);
    if (existingOrderId is { } alreadyPlacedOrderId)
      return await ReadPlacedOrderAsync(alreadyPlacedOrderId, cancellationToken);

    var festival = await _runningFestival.FindAsync(cancellationToken);
    if (festival is null)
      return Refusal.Order.NoRunningFestival();

    if (!await _database.Tables.AsNoTracking().AnyAsync(table => table.Id == request.TableId && table.FestivalId == festival.Id, cancellationToken))
      return Refusal.Order.TableNotAtTheRunningFestival(festival.Id, request.TableId);

    ErrorOr<IReadOnlyList<OrderItem>> routing = await RouteItemsAsync(festival.Id, request.Items!, cancellationToken);
    if (routing.IsError)
      return routing.Errors;

    var order = await BuildOrderAsync(request, staffMemberId, festival.Id, routing.Value, cancellationToken);

    _database.Orders.Add(order);
    await _database.SaveChangesAsync(cancellationToken);

    foreach (var item in routing.Value)
      _itemLifecycle.PointAtFirstStatus(item);

    await _database.SaveChangesAsync(cancellationToken);

    await _soldOutMarker.MarkItemsWithoutEnoughStockSoldOutAsync(festival.Id, cancellationToken);

    return await ReadPlacedOrderAsync(order.Id, cancellationToken);
  }

  private async Task<PlacedOrderView> ReadPlacedOrderAsync(Guid orderId, CancellationToken cancellationToken)
  {
    var order = await _database.Orders.AsNoTracking()
                               .Include(candidate => candidate.StationOrders)
                               .ThenInclude(stationOrder => stationOrder.Station)
                               .Include(candidate => candidate.StationOrders)
                               .ThenInclude(stationOrder => stationOrder.Items)
                               .ThenInclude(item => item.CurrentStatus)
                               .FirstAsync(candidate => candidate.Id == orderId, cancellationToken);

    return order.Adapt<PlacedOrderView>(_mapping) with
           {
             Status = _orderService.StatusOf(order),
             TotalCents = _orderService.TotalCentsOf(order)
           };
  }

  private async Task<ErrorOr<IReadOnlyList<OrderItem>>> RouteItemsAsync(Guid festivalId, IReadOnlyList<OrderItemRequest> itemRequests, CancellationToken cancellationToken)
  {
    List<Guid> catalogItemIds = itemRequests.Select(itemRequest => itemRequest.CatalogItemId).Distinct().ToList();

    Dictionary<Guid, CatalogItem> catalogItems = await _database.CatalogItems.AsNoTracking().Where(item => catalogItemIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
    Dictionary<Guid, FestivalCatalogItem> menuRows = await _database.FestivalCatalogItems.AsNoTracking().Where(menuRow => menuRow.FestivalId == festivalId && catalogItemIds.Contains(menuRow.CatalogItemId)).ToDictionaryAsync(menuRow => menuRow.CatalogItemId, cancellationToken);
    List<ItemStationAssignment> assignments = await _database.ItemStationAssignments.AsNoTracking().Where(assignment => assignment.FestivalId == festivalId && catalogItemIds.Contains(assignment.CatalogItemId)).ToListAsync(cancellationToken);
    List<Station> stationsAtTheFestival = await _database.FestivalStations.AsNoTracking().ActiveAt(festivalId).OrderBy(link => link.Station.SortOrder).Select(link => link.Station).ToListAsync(cancellationToken);

    Dictionary<Guid, StationOrder> stationOrdersByStationId = [];
    List<OrderItem> routedItems = [];
    List<Error> refusedLines = [];

    foreach (var itemRequest in itemRequests)
    {
      if (!catalogItems.TryGetValue(itemRequest.CatalogItemId, out var catalogItem))
      {
        refusedLines.Add(Refusal.Order.UnknownCatalogItemId(itemRequest.CatalogItemId));
        continue;
      }

      if (!catalogItem.IsActive || menuRows.GetValueOrDefault(catalogItem.Id) is { IsAvailable: false })
      {
        refusedLines.Add(Refusal.Order.ItemNotAvailable(catalogItem.Id, catalogItem.Name));
        continue;
      }

      ErrorOr<Station> station = _routingResolver.Resolve(catalogItem, assignments, stationsAtTheFestival, itemRequest.StationId);

      if (station.IsError)
      {
        refusedLines.AddRange(station.Errors);
        continue;
      }

      routedItems.Add(BuildItemIntoStationOrder(itemRequest, catalogItem, StationOrderFor(stationOrdersByStationId, festivalId, station.Value.Id)));
    }

    if (refusedLines.Count > 0)
      return refusedLines;

    return routedItems;
  }

  private StationOrder StationOrderFor(Dictionary<Guid, StationOrder> stationOrdersByStationId, Guid festivalId, Guid stationId)
  {
    if (stationOrdersByStationId.TryGetValue(stationId, out var known))
      return known;

    StationOrder created = new()
                           {
                             Id = Guid.NewGuid(),
                             OrderId = Guid.Empty,
                             FestivalId = festivalId,
                             StationId = stationId,
                             StationOrderNumber = 0,
                             DeliveryMode = DeliveryMode.Together
                           };

    stationOrdersByStationId.Add(stationId, created);

    return created;
  }

  private OrderItem BuildItemIntoStationOrder(OrderItemRequest itemRequest, CatalogItem catalogItem, StationOrder stationOrder)
  {
    OrderItem orderItem = new()
                          {
                            Id = Guid.NewGuid(),
                            StationOrderId = stationOrder.Id,
                            CatalogItemId = catalogItem.Id,
                            ItemName = catalogItem.Name,
                            UnitPriceCents = itemRequest.UnitPriceCents,
                            Note = itemRequest.Note,
                            StationOrder = stationOrder
                          };

    stationOrder.Items.Add(orderItem);

    return orderItem;
  }

  private async Task<Order> BuildOrderAsync(PlaceOrderRequest request, Guid staffMemberId, Guid festivalId, IReadOnlyCollection<OrderItem> routedItems, CancellationToken cancellationToken)
  {
    var createdAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
    var globalOrderNumber = await _numberAllocator.AllocateGlobalOrderNumberAsync(festivalId, cancellationToken);

    Order order = new()
                  {
                    Id = Guid.NewGuid(),
                    ClientOrderId = request.ClientOrderId,
                    FestivalId = festivalId,
                    GlobalOrderNumber = globalOrderNumber,
                    StaffMemberId = staffMemberId,
                    TableId = request.TableId,
                    CreatedAtUtc = createdAtUtc
                  };

    Dictionary<Guid, DeliveryMode> deliveryModesByStationId = (request.DeliveryModes ?? []).GroupBy(mode => mode.StationId).ToDictionary(group => group.Key, group => group.Last().DeliveryMode);

    foreach (var routedItem in routedItems)
    {
      _itemLifecycle.AppendStatus(routedItem, OrderItemState.Open, new(staffMemberId, null), createdAtUtc);

      var stationOrder = routedItem.StationOrder;

      if (order.StationOrders.Contains(stationOrder))
        continue;

      stationOrder.OrderId = order.Id;
      stationOrder.Order = order;
      stationOrder.StationOrderNumber = await _numberAllocator.AllocateStationOrderNumberAsync(festivalId, stationOrder.StationId, cancellationToken);
      stationOrder.DeliveryMode = DeliveryModeFor(deliveryModesByStationId, stationOrder.StationId);

      order.StationOrders.Add(stationOrder);
    }

    return order;
  }

  private DeliveryMode DeliveryModeFor(IReadOnlyDictionary<Guid, DeliveryMode> deliveryModesByStationId, Guid stationId)
  {
    if (deliveryModesByStationId.TryGetValue(stationId, out var chosenMode))
      return chosenMode;

    return DeliveryMode.Together;
  }
}
