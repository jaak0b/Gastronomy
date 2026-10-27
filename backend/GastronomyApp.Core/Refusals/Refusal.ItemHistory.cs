using ErrorOr;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class ItemHistory
  {
    public static Error UnknownItem(Guid orderItemId)
    {
      return UnprocessableEntity(RefusalMessageKeys.ItemHistoryUnknownItem, $"The history of the order item {orderItemId} was asked for, but the laptop holds no such item.", new() { [MetadataKeys.ProblemCode] = ProblemCodes.UnprocessableEntity });
    }
  }
}
