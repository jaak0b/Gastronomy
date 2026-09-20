using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class UnreachableCaseTest
{
  [Test]
  public void Throw_GivenAnUnhandledValue_ThrowsNamingTheValue()
  {
    UnreachableCase unreachableCase = new();

    var thrown = Assert.Throws<InvalidOperationException>(() => unreachableCase.Throw<string>(OrderStatus.Fulfilled));

    Assert.That(thrown!.Message, Does.Contain(OrderStatus.Fulfilled.ToString()));
  }

  [Test]
  public void Throw_GivenAnUnhandledNumber_ThrowsNamingTheValue()
  {
    UnreachableCase unreachableCase = new();

    var thrown = Assert.Throws<InvalidOperationException>(() => unreachableCase.Throw<bool>(8));

    Assert.That(thrown!.Message, Does.Contain("8"));
  }
}
