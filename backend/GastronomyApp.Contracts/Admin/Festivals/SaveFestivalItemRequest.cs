using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record SaveFestivalItemRequest
{
  [Range(0, 99999, ErrorMessage = RefusalMessageKeys.AdminItemPriceOutOfRange)]
  public required int PriceCents { get; init; }

  public required IReadOnlyList<Guid>? StationIds { get; init; }
}
