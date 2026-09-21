using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

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

  public async Task<Result<IReadOnlyList<OrderItem>, OrderValidationFailure>> BuildRoutedItemsAsync(Guid festivalId, IReadOnlyList<OrderItemRequest> itemRequests, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(itemRequests);

    IReadOnlyCollection<Station> stationsAtTheFestival = await _stationRepository.FindAtFestivalAsync(festivalId, cancellationToken);

    Dictionary<Guid, StationOrder> stationOrdersByStationId = [];
    List<OrderItem> routedItems = [];

    foreach (var itemRequest in itemRequests)
    {
      var catalogItem = await _catalogItemRepository.FindByIdAsync(itemRequest.CatalogItemId, cancellationToken);
      if (catalogItem is null)
      {
        return Result<IReadOnlyList<OrderItem>, OrderValidationFailure>.Failed(new()
                                                                               {
                                                                                 Reason = OrderValidationFailureReason.UnknownCatalogItemId,
                                                                                 OffendingCatalogItemId = itemRequest.CatalogItemId
                                                                               });
      }

      var menuRow = await _catalogItemRepository.FindMenuRowAsync(festivalId, itemRequest.CatalogItemId, cancellationToken);
      if (!catalogItem.IsActive || menuRow is { IsAvailable: false })
      {
        return Result<IReadOnlyList<OrderItem>, OrderValidationFailure>.Failed(new()
                                                                               {
                                                                                 Reason = OrderValidationFailureReason.ItemNotAvailable,
                                                                                 OffendingCatalogItemId = itemRequest.CatalogItemId,
                                                                                 OffendingCatalogItemName = catalogItem.Name
                                                                               });
      }

      IReadOnlyCollection<ItemStationAssignment> assignments = await _catalogItemRepository.FindAssignmentsAsync(festivalId, itemRequest.CatalogItemId, cancellationToken);

      Result<Station, Failure<RoutingFailureReason>> routing = _routingResolver.Resolve(itemRequest.CatalogItemId, assignments, stationsAtTheFestival, itemRequest.StationId);

      if (!routing.IsSuccess)
      {
        return Result<IReadOnlyList<OrderItem>, OrderValidationFailure>.Failed(new()
                                                                               {
                                                                                 Reason = TranslateRoutingFailureReason(routing.Failure.Reason),
                                                                                 OffendingCatalogItemId = itemRequest.CatalogItemId,
                                                                                 OffendingCatalogItemName = catalogItem.Name
                                                                               });
      }

      routedItems.Add(BuildItemIntoStationOrder(itemRequest, catalogItem, StationOrderFor(stationOrdersByStationId, festivalId, routing.Value.Id)));
    }

    return Result<IReadOnlyList<OrderItem>, OrderValidationFailure>.Success(routedItems);
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

  private OrderValidationFailureReason TranslateRoutingFailureReason(RoutingFailureReason routingFailureReason)
  {
    return routingFailureReason switch
           {
             RoutingFailureReason.ItemHasNoStation => OrderValidationFailureReason.ItemHasNoStation,
             RoutingFailureReason.StationRequired => OrderValidationFailureReason.StationRequired,
             RoutingFailureReason.StationNotAssignedToItem => OrderValidationFailureReason.StationNotAssignedToItem,
             RoutingFailureReason.ChosenStationNoLongerPreparesTheItem => OrderValidationFailureReason.ChosenStationNoLongerPreparesTheItem,
             _ => new UnreachableCase().Throw<OrderValidationFailureReason>(routingFailureReason)
           };
  }
}
