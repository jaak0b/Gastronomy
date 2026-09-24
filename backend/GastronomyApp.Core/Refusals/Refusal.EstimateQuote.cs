using ErrorOr;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class EstimateQuote
  {
    public static Error UnknownCatalogItem(Guid catalogItemId)
    {
      return BadRequest(RefusalMessageKeys.OrderCannotBeProcessed,
                        $"The estimate request names catalog item {catalogItemId}, which does not exist.",
                        new()
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed,
                          [MetadataKeys.CatalogItemId] = catalogItemId.ToString()
                        });
    }

    public static Error UnknownStation(Guid stationId)
    {
      return BadRequest(RefusalMessageKeys.OrderCannotBeProcessed,
                        $"The estimate request names station {stationId}, which is not active at the running festival.",
                        new()
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed,
                          [MetadataKeys.StationId] = stationId.ToString()
                        });
    }
  }
}
