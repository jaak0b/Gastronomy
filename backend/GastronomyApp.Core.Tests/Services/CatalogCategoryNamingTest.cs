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
  public void ToCleanedName_NameTypedWithSpacesAroundIt_KeepsOnlyTheName()
  {
    Assert.That(_naming.ToCleanedName(" Kaffee "), Is.EqualTo("Kaffee"));
  }

  [Test]
  public void ToNormalizedName_TheSameNameWrittenWithSpacesAndInAnotherCasing_IsAlwaysTheSameValue()
  {
    var asItIsUsuallyWritten = _naming.ToNormalizedName($"Getr{SmallUmlautA}nke");

    Assert.Multiple(() =>
                    {
                      Assert.That(_naming.ToNormalizedName($" Getr{SmallUmlautA}nke "), Is.EqualTo(asItIsUsuallyWritten));
                      Assert.That(_naming.ToNormalizedName($"GETR{CapitalUmlautA}NKE"), Is.EqualTo(asItIsUsuallyWritten));
                    });
  }

  [Test]
  public void ToNormalizedName_TwoNamesThatAreGenuinelyDifferent_AreDifferentValues()
  {
    Assert.That(_naming.ToNormalizedName("Kaffee"), Is.Not.EqualTo(_naming.ToNormalizedName("Kuchen")));
  }
}
