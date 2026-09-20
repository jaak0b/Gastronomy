using GastronomyApp.Api.Options;

namespace GastronomyApp.Api.Tests.Options;

[TestFixture]
public sealed class AppLanguageTest
{
  [Test]
  public void Current_SetToNull_ThrowsArgumentException()
  {
    AppLanguage language = new();

    var failure = Assert.Throws<ArgumentNullException>(() => language.Current = null!)!;

    Assert.That(failure.ParamName, Is.EqualTo("value"));
  }

  [Test]
  public void Current_SetToBlank_ThrowsArgumentException()
  {
    AppLanguage language = new();

    var failure = Assert.Throws<ArgumentException>(() => language.Current = "   ")!;

    Assert.That(failure.ParamName, Is.EqualTo("value"));
  }
}
