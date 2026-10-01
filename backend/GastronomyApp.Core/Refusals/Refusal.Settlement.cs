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

    public static Error SelectionSpansSeveralTables(IReadOnlyList<string> tableNames)
    {
      return BadRequest(RefusalMessageKeys.SettlementCannotBeProcessed,
                        $"The items sent belong to the tables {string.Join(", ", tableNames)}. The open items screen holds every other table back once one of them has something ticked, so this call did not come from that screen.",
                        new() { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }

    public static Error NoRunningFestival()
    {
      return BadRequest(RefusalMessageKeys.SettlementCannotBeProcessed, "No festival is running, so nothing was settled.", new() { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }

    public static Error DuplicateOrderItemId(Guid orderItemId)
    {
      return BadRequest(RefusalMessageKeys.SettlementCannotBeProcessed,
                        $"The settlement names the order item {orderItemId} more than once. The open items screen ticks each item at most once, so this call did not come from that screen.",
                        new() { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }
  }
}
