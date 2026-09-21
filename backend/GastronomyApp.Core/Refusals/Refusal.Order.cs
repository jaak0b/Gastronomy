using ErrorOr;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class Order
  {
    public static Error UnknownCatalogItemId(Guid catalogItemId)
    {
      return UnprocessableEntity("order.unknownItem",
                                 $"The order names the catalog item {catalogItemId}, which no longer exists.",
                                 new()
                                 {
                                   [MetadataKeys.ProblemCode] = ProblemCodes.UnprocessableEntity,
                                   [MetadataKeys.CatalogItemId] = catalogItemId.ToString()
                                 });
    }

    public static Error ItemNotAvailable(Guid catalogItemId, string itemName)
    {
      return UnprocessableEntity("catalog.itemSoldOut",
                                 $"The item {itemName} ({catalogItemId}) is switched off or sold out at the running festival.",
                                 new()
                                 {
                                   [MetadataKeys.ProblemCode] = ProblemCodes.UnprocessableEntity,
                                   [MetadataKeys.CatalogItemId] = catalogItemId.ToString(),
                                   [MetadataKeys.Name] = itemName
                                 });
    }

    public static Error StationNotAssignedToItem(Guid catalogItemId, string itemName)
    {
      return UnprocessableEntity("catalog.itemSoldOut",
                                 $"The station the order chose for the item {itemName} ({catalogItemId}) does not prepare it at the running festival.",
                                 new()
                                 {
                                   [MetadataKeys.ProblemCode] = ProblemCodes.UnprocessableEntity,
                                   [MetadataKeys.CatalogItemId] = catalogItemId.ToString(),
                                   [MetadataKeys.Name] = itemName
                                 });
    }

    public static Error ChosenStationNoLongerPreparesTheItem(Guid catalogItemId, string itemName)
    {
      return UnprocessableEntity("catalog.itemSoldOut",
                                 $"The station the order chose for the item {itemName} ({catalogItemId}) is switched off, so it no longer prepares it.",
                                 new()
                                 {
                                   [MetadataKeys.ProblemCode] = ProblemCodes.UnprocessableEntity,
                                   [MetadataKeys.CatalogItemId] = catalogItemId.ToString(),
                                   [MetadataKeys.Name] = itemName
                                 });
    }

    public static Error StationRequired(Guid catalogItemId, string itemName)
    {
      return UnprocessableEntity(RefusalMessageKeys.OrderCannotBeProcessed,
                                 $"Several stations prepare the item {itemName} ({catalogItemId}) and the order names none of them. The order screen makes the waiter pick one, so this call did not come from that screen.",
                                 new() { [MetadataKeys.ProblemCode] = ProblemCodes.UnprocessableEntity });
    }

    public static Error ItemHasNoStation(Guid catalogItemId, string itemName)
    {
      return UnprocessableEntity(RefusalMessageKeys.OrderCannotBeProcessed, $"No station at the running festival prepares the item {itemName} ({catalogItemId}), so the order cannot be routed anywhere.", new() { [MetadataKeys.ProblemCode] = ProblemCodes.UnprocessableEntity });
    }

    public static Error NoRunningFestival()
    {
      return UnprocessableEntity(RefusalMessageKeys.OrderCannotBeProcessed, "No festival is running, so the order belongs to nothing and was not stored.", new() { [MetadataKeys.ProblemCode] = ProblemCodes.UnprocessableEntity });
    }

    public static Error OrderNumberCouldNotBeAllocated()
    {
      return UnprocessableEntity(RefusalMessageKeys.OrderCannotBeProcessed, "Every attempt to take the next order number lost the race to another writer, so the order was not stored.", new() { [MetadataKeys.ProblemCode] = ProblemCodes.UnprocessableEntity });
    }

    public static Error SettlementCannotBeProcessed(Error settlementRefusal)
    {
      return BadRequest(RefusalMessageKeys.SettlementCannotBeProcessed, $"The settlement sent with the order was refused: {settlementRefusal.Description}", new() { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }
  }
}
