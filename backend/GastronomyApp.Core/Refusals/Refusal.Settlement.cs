using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class Settlement
  {
    public static Error PaymentNoticeMissing(Guid orderItemId)
    {
      return BadRequest(RefusalMessageKeys.SettlementCannotBeProcessed,
                        $"The order item {orderItemId} was settled below its displayed price without a typed reason. The open items screen asks for that reason, so this call did not come from that screen.",
                        new() { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }

    public static Error PaymentMethodMissingForMoneyPaid(PaymentMethod paymentMethod)
    {
      return BadRequest(RefusalMessageKeys.SettlementCannotBeProcessed,
                        $"The settlement collects money but names the payment method {paymentMethod}. The open items screen sends cash or card whenever money is collected, so this call did not come from that screen.",
                        new() { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }

    public static Error PaymentMethodNamedForNothingPaid(PaymentMethod paymentMethod)
    {
      return BadRequest(RefusalMessageKeys.SettlementCannotBeProcessed,
                        $"The settlement collects no money but names the payment method {paymentMethod}. The open items screen sends none when nothing is collected, so this call did not come from that screen.",
                        new() { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }

    public static Error UnknownOrderItemId(Guid orderItemId)
    {
      return UnprocessableEntity("errors.settlement.unknownItem",
                                 $"The settlement names the order item {orderItemId}, which the running festival does not hold.",
                                 new()
                                 {
                                   [MetadataKeys.ProblemCode] = ProblemCodes.UnprocessableEntity,
                                   [MetadataKeys.OrderItemId] = orderItemId.ToString()
                                 });
    }

    public static Error ItemWasCancelled(Guid orderItemId, string itemName)
    {
      return Conflict("errors.settlement.itemWasCancelled",
                      $"The settlement names the order item {orderItemId}, which a waiter has cancelled in the meantime.",
                      new()
                      {
                        [MetadataKeys.ProblemCode] = "ItemWasCancelled",
                        [MetadataKeys.Name] = itemName
                      });
    }

    public static Error SelectionSpansSeveralTables(IReadOnlyList<Guid> tableIds)
    {
      return BadRequest(RefusalMessageKeys.SettlementCannotBeProcessed,
                        $"The items sent belong to the tables {string.Join(", ", tableIds)}. The open items screen shows the items of one table only, so this call did not come from that screen.",
                        new() { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }

    public static Error TableNotAtTheRunningFestival(Guid tableId)
    {
      return NotFound("TableNotAtTheRunningFestival", $"The table {tableId} whose orders were asked for is not a table of the running festival.");
    }

    public static Error NoRunningFestival()
    {
      return BadRequest(RefusalMessageKeys.SettlementCannotBeProcessed, "No festival is running, so the request about open items was refused.", new() { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }

    public static Error DuplicateOrderItemId(Guid orderItemId)
    {
      return BadRequest(RefusalMessageKeys.SettlementCannotBeProcessed,
                        $"The settlement names the order item {orderItemId} more than once. The open items screen ticks each item at most once, so this call did not come from that screen.",
                        new() { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }
  }
}
