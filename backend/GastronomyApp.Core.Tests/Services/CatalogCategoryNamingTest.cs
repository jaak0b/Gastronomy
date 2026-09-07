using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class CatalogCategoryNamingTest
{
  private const string SmallUmlautA = "ä";
  private const string CapitalUmlautA = "Ä";

  [SetUp]
  public void SetUp()
  {
    _naming = new();
  }

  private CatalogCategoryNaming _naming = null!;

  [Test]
  public void Cleaned_NameTypedWithSpacesAroundIt_KeepsOnlyTheName()
  {
    Assert.That(_naming.Cleaned(" Kaffee "), Is.EqualTo("Kaffee"));
  }

  [Test]
  public void Normalized_TheSameNameWrittenWithSpacesAndInAnotherCasing_IsAlwaysTheSameValue()
  {
    var asItIsUsuallyWritten = _naming.Normalized($"Getr{SmallUmlautA}nke");

    Assert.Multiple(() =>
                    {
                      Assert.That(_naming.Normalized($" Getr{SmallUmlautA}nke "), Is.EqualTo(asItIsUsuallyWritten));
                      Assert.That(_naming.Normalized($"GETR{CapitalUmlautA}NKE"), Is.EqualTo(asItIsUsuallyWritten));
                    });
  }

  [Test]
  public void Normalized_TwoNamesThatAreGenuinelyDifferent_AreDifferentValues()
  {
    Assert.That(_naming.Normalized("Kaffee"), Is.Not.EqualTo(_naming.Normalized("Kuchen")));
  }
}
