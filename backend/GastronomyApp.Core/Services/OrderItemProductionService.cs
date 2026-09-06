using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed record ProductionStatusChangeRequest
{
  public required IReadOnlyList<Guid> OrderItemIds { get; init; }

  public required ProductionStatus TargetStatus { get; init; }
}

public sealed class OrderItemProductionService
{
  private readonly ProductionStatusTransition _transition = new();

  public void RecordPlacement(OrderItem item, DateTime placedAtUtc)
  {
    ArgumentNullException.ThrowIfNull(item);

    Move(item, ProductionStatus.Waiting, placedAtUtc);
  }

  public Result<ProductionStatusChangeResult, ProductionStatusFailure> Advance(ProductionStatusChangeRequest request,
                                                                               IReadOnlyCollection<OrderItem> knownItems,
                                                                               DateTime changedAtUtc)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(knownItems);

    List<Guid> selectedIds = [.. request.OrderItemIds.Distinct()];

    if (selectedIds.Count == 0)
    {
      return Result<ProductionStatusChangeResult, ProductionStatusFailure>.Failed(new()
                                                                                  {
                                                                                    Reason = ProductionStatusFailureReason.NoItemsSelected
                                                                                  });
    }

    Dictionary<Guid, OrderItem> itemsById = knownItems.ToDictionary(item => item.Id);
    List<OrderItem> selected = [];

    foreach (var orderItemId in selectedIds)
    {
      if (!itemsById.TryGetValue(orderItemId, out var item))
      {
        return Result<ProductionStatusChangeResult, ProductionStatusFailure>.Failed(new()
                                                                                    {
                                                                                      Reason = ProductionStatusFailureReason.UnknownOrderItemId,
                                                                                      OffendingOrderItemId = orderItemId
                                                                                    });
      }

      if (!_transition.IsAllowed(item.ProductionStatus, request.TargetStatus))
      {
        return Result<ProductionStatusChangeResult, ProductionStatusFailure>.Failed(new()
                                                                                    {
                                                                                      Reason = ProductionStatusFailureReason.TransitionNotAllowed,
                                                                                      OffendingOrderItemId = orderItemId
                                                                                    });
      }

      selected.Add(item);
    }

    foreach (var item in selected)
    {
      Move(item, request.TargetStatus, changedAtUtc);
    }

    return Result<ProductionStatusChangeResult, ProductionStatusFailure>.Success(new() { ChangedItems = selected });
  }

  private void Move(OrderItem item, ProductionStatus status, DateTime changedAtUtc)
  {
    item.ProductionStatus = status;
    item.StatusChanges.Add(new()
                           {
                             Id = Guid.NewGuid(),
                             OrderItemId = item.Id,
                             Status = status,
                             ChangedAtUtc = changedAtUtc
                           });
  }
}
