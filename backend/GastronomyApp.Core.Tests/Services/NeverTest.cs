using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class NeverTest
{
  [Test]
  public void OfType_GivenAnUnhandledValue_ThrowsNamingTheValue()
  {
    Never never = new();

    var thrown = Assert.Throws<InvalidOperationException>(() => never.OfType<string>(ProductionStatus.Finished));

    Assert.That(thrown!.Message, Does.Contain(ProductionStatus.Finished.ToString()));
  }

  [Test]
  public void OfType_GivenAnUnhandledNumber_ThrowsNamingTheValue()
  {
    Never never = new();

    var thrown = Assert.Throws<InvalidOperationException>(() => never.OfType<bool>(8));

    Assert.That(thrown!.Message, Does.Contain("8"));
  }
}
