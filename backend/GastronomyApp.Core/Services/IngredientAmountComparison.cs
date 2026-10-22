namespace GastronomyApp.Core.Services;

public sealed class IngredientAmountComparison
{
  private const double RelativeTolerance = 1e-10;

  public bool IsAtLeast(double amount, double threshold)
  {
    if (double.IsNaN(amount))
      throw new ArgumentException("An ingredient amount is always a number.", nameof(amount));

    if (double.IsNaN(threshold))
      throw new ArgumentException("An ingredient amount is always a number.", nameof(threshold));

    var scale = Math.Max(1, Math.Max(Math.Abs(amount), Math.Abs(threshold)));

    return amount >= threshold - RelativeTolerance * scale;
  }
}
