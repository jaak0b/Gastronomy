using ErrorOr;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class Cancellation
  {
    public static Error NoRunningFestival()
    {
      return BadRequest(RefusalMessageKeys.CancellationCannotBeProcessed,
                        "No festival is running, so the cancellation was refused. The waiter's phone offers cancelling only on a table of the running festival, so this call did not come from that screen.",
                        new() { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }

    public static Error NothingSelected()
    {
      return BadRequest(RefusalMessageKeys.CancellationCannotBeProcessed, "The cancellation names no order item. The waiter's phone sends a cancellation only with items selected, so this call did not come from that screen.", new() { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }

    public static Error DuplicateOrderItemId(Guid orderItemId)
    {
      return BadRequest(RefusalMessageKeys.CancellationCannotBeProcessed,
                        $"The cancellation names the order item {orderItemId} more than once. The waiter's phone selects each item at most once, so this call did not come from that screen.",
                        new() { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }

    public static Error UnknownOrderItemId(Guid orderItemId)
    {
      return UnprocessableEntity("errors.cancellation.unknownItem",
                                 $"The cancellation names the order item {orderItemId}, which the running festival does not hold.",
                                 new()
                                 {
                                   [MetadataKeys.ProblemCode] = ProblemCodes.UnprocessableEntity,
                                   [MetadataKeys.OrderItemId] = orderItemId.ToString()
                                 });
    }

    public static Error AlreadyCancelled(Guid orderItemId, string itemName)
    {
      return Conflict("errors.cancellation.alreadyCancelled",
                      $"The order item {orderItemId} was cancelled before, so it is not cancelled again and no money is handed back twice.",
                      new()
                      {
                        [MetadataKeys.ProblemCode] = "ItemAlreadyCancelled",
                        [MetadataKeys.Name] = itemName
                      });
    }

    public static Error SelectionSpansSeveralTables(IReadOnlyList<Guid> tableIds)
    {
      return BadRequest(RefusalMessageKeys.CancellationCannotBeProcessed,
                        $"The items sent for cancelling belong to the tables {string.Join(", ", tableIds)}. The waiter's phone shows the items of one table only, so this call did not come from that screen.",
                        new() { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }
  }
}
