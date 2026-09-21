using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Admin.Catalog;

public sealed record SaveCategoryRequest
{
  [RequiredText(ErrorMessage = RefusalMessageKeys.AdminCategoryNameMissing)]
  public required string? Name { get; init; }

  [Required(ErrorMessage = RefusalMessageKeys.AdminCategoryColourInvalid)]
  [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = RefusalMessageKeys.AdminCategoryColourInvalid)]
  public required string? ColourHex { get; init; }
}
