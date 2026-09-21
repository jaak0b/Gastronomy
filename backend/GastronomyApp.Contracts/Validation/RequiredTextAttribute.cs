using System.ComponentModel.DataAnnotations;

namespace GastronomyApp.Contracts.Validation;

[AttributeUsage(AttributeTargets.Property)]
public sealed class RequiredTextAttribute : RequiredAttribute
{
  override public bool IsValid(object? value)
  {
    if (value is string text)
      return !string.IsNullOrWhiteSpace(text);

    return base.IsValid(value);
  }
}
