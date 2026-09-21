using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Admin.Catalog;

public sealed record SaveItemRequest
{
  [RequiredText(ErrorMessage = RefusalMessageKeys.AdminItemNameMissing)]
  public required string? Name { get; init; }

  public required Guid? CategoryId { get; init; }

  public required int SortOrder { get; init; }

  public double? ProductionMinutes { get; init; }

  public bool IsQueueIndependent { get; init; }
}
