using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class CatalogCategoryColourTest
{
  [SetUp]
  public void SetUp()
  {
    _colour = new();
  }

  private CatalogCategoryColour _colour = null!;

  [TestCase("#C62828")]
  [TestCase("#000000")]
  [TestCase("#ffffff")]
  [TestCase("#6d4C41")]
  public void IsWellFormed_HashFollowedBySixHexDigits_IsAccepted(string colourHex)
  {
    Assert.That(_colour.IsWellFormed(colourHex), Is.True);
  }

  [TestCase(null)]
  [TestCase("")]
  [TestCase("   ")]
  [TestCase("C62828")]
  [TestCase("#C6282")]
  [TestCase("#C628288")]
  [TestCase("#C6282G")]
  [TestCase("#C628 8")]
  [TestCase("braun")]
  [TestCase("rgb(198,40,40)")]
  public void IsWellFormed_AnythingElse_IsRefused(string? colourHex)
  {
    Assert.That(_colour.IsWellFormed(colourHex), Is.False);
  }
}
