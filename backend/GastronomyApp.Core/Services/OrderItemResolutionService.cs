using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Services;

public sealed class OrderItemResolutionService
{
  private readonly ICatalogItemRepository _catalogItemRepository;
  private readonly OrderRoutingResolver _routingResolver;
  private readonly IStationRepository _stationRepository;

  public OrderItemResolutionService(ICatalogItemRepository catalogItemRepository, IStationRepository stationRepository, OrderRoutingResolver routingResolver)
  {
    _catalogItemRepository = catalogItemRepository;
    _stationRepository = stationRepository;
    _routingResolver = routingResolver;
  }

  public async Task<ErrorOr<IReadOnlyList<OrderItem>>> BuildRoutedItemsAsync(Guid festivalId, IReadOnlyList<OrderItemRequest> itemRequests, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(itemRequests);

    IReadOnlyCollection<Station> stationsAtTheFestival = await _stationRepository.FindAtFestivalAsync(festivalId, cancellationToken);

    Dictionary<Guid, StationOrder> stationOrdersByStationId = [];
    List<OrderItem> routedItems = [];
    List<Error> refusedLines = [];

    foreach (var itemRequest in itemRequests)
    {
      var catalogItem = await _catalogItemRepository.FindByIdAsync(itemRequest.CatalogItemId, cancellationToken);
      if (catalogItem is null)
      {
        refusedLines.Add(Refusal.Order.UnknownCatalogItemId(itemRequest.CatalogItemId));
        continue;
      }

      var menuRow = await _catalogItemRepository.FindMenuRowAsync(festivalId, itemRequest.CatalogItemId, cancellationToken);
      if (!catalogItem.IsActive || menuRow is { IsAvailable: false })
      {
        refusedLines.Add(Refusal.Order.ItemNotAvailable(catalogItem.Id, catalogItem.Name));
        continue;
      }

      IReadOnlyCollection<ItemStationAssignment> assignments = await _catalogItemRepository.FindAssignmentsAsync(festivalId, itemRequest.CatalogItemId, cancellationToken);

      ErrorOr<Station> routing = _routingResolver.Resolve(catalogItem, assignments, stationsAtTheFestival, itemRequest.StationId);

      if (routing.IsError)
      {
        refusedLines.AddRange(routing.Errors);
        continue;
      }

      routedItems.Add(BuildItemIntoStationOrder(itemRequest, catalogItem, StationOrderFor(stationOrdersByStationId, festivalId, routing.Value.Id)));
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
}
