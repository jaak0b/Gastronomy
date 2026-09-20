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

  public async Task<Result<IReadOnlyList<ResolvedOrderItem>, OrderValidationFailure>> ResolveAsync(Guid festivalId, IReadOnlyList<OrderAcceptanceItemRequest> itemRequests, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(itemRequests);

    IReadOnlyCollection<Station> stationsAtTheFestival = await _stationRepository.FindAtFestivalAsync(festivalId, cancellationToken);

    List<ResolvedOrderItem> resolvedItems = [];

    foreach (var itemRequest in itemRequests)
    {
      var catalogItem = await _catalogItemRepository.FindByIdAsync(itemRequest.CatalogItemId, cancellationToken);
      if (catalogItem is null)
      {
        return Result<IReadOnlyList<ResolvedOrderItem>, OrderValidationFailure>.Failed(new()
                                                                                       {
                                                                                         Reason = OrderValidationFailureReason.UnknownCatalogItemId,
                                                                                         OffendingCatalogItemId = itemRequest.CatalogItemId
                                                                                       });
      }

      var menuRow = await _catalogItemRepository.FindMenuRowAsync(festivalId, itemRequest.CatalogItemId, cancellationToken);
      if (!catalogItem.IsActive || menuRow is { IsAvailable: false })
      {
        return Result<IReadOnlyList<ResolvedOrderItem>, OrderValidationFailure>.Failed(new()
                                                                                       {
                                                                                         Reason = OrderValidationFailureReason.ItemNotAvailable,
                                                                                         OffendingCatalogItemId = itemRequest.CatalogItemId,
                                                                                         OffendingCatalogItemName = catalogItem.Name
                                                                                       });
      }

      IReadOnlyCollection<ItemStationAssignment> assignments = await _catalogItemRepository.FindAssignmentsAsync(festivalId, itemRequest.CatalogItemId, cancellationToken);

      Result<RoutingDecision, RoutingFailure> routing = _routingResolver.Resolve(itemRequest.CatalogItemId, assignments, stationsAtTheFestival, itemRequest.StationId);

      if (!routing.IsSuccess)
      {
        return Result<IReadOnlyList<ResolvedOrderItem>, OrderValidationFailure>.Failed(new()
                                                                                       {
                                                                                         Reason = TranslateRoutingFailureReason(routing.Failure.Reason),
                                                                                         OffendingCatalogItemId = itemRequest.CatalogItemId,
                                                                                         OffendingCatalogItemName = catalogItem.Name
                                                                                       });
      }

      resolvedItems.Add(new()
                        {
                          Request = itemRequest,
                          CatalogItem = catalogItem,
                          Decision = routing.Value
                        });
    }

    return Result<IReadOnlyList<ResolvedOrderItem>, OrderValidationFailure>.Success(resolvedItems);
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
