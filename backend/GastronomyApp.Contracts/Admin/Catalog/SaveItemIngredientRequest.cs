using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Admin.Catalog;

public sealed record SaveItemIngredientRequest
{
  [Required(ErrorMessage = RefusalMessageKeys.AdminIngredientAmountInvalid)]
  public required double? Amount { get; init; }
}
