using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class IngredientAmountComparisonTest
{
  private readonly IngredientAmountComparison _comparison = new();

  [TestCase(0.32999999999999996, 0.33)]
  [TestCase(32.99999999999999, 33d)]
  [TestCase(999_999.9999999999, 1_000_000d)]
  public void IsAtLeast_ASumThatMissesTheThresholdOnlyByRounding_IsAtLeastTheThreshold(double amount, double threshold)
  {
    Assert.That(_comparison.IsAtLeast(amount, threshold), Is.True);
  }

  [TestCase(0.329, 0.33)]
  [TestCase(32.999, 33d)]
  [TestCase(999_999.999, 1_000_000d)]
  public void IsAtLeast_AnAmountAThousandthBelowTheThreshold_IsNotAtLeastTheThreshold(double amount, double threshold)
  {
    Assert.That(_comparison.IsAtLeast(amount, threshold), Is.False);
  }

  [Test]
  public void IsAtLeast_AnAmountAboveTheThreshold_IsAtLeastTheThreshold()
  {
    Assert.That(_comparison.IsAtLeast(0.34, 0.33), Is.True);
  }

  [Test]
  public void IsAtLeast_NotANumber_IsRefused()
  {
    Assert.That(() => _comparison.IsAtLeast(double.NaN, 1), Throws.ArgumentException.With.Property(nameof(ArgumentException.ParamName)).EqualTo("amount"));
  }
}
