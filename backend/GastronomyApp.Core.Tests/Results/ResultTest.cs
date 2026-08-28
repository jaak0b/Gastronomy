using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Tests.Results;

[TestFixture]
public sealed class ResultTest
{
  [Test]
  public void Success_GivenValue_IsSuccessAndCarriesValue()
  {
    Result<string, int> result = Result<string, int>.Success("routed");

    Assert.Multiple(() =>
    {
      Assert.That(result.IsSuccess, Is.True);
      Assert.That(result.Value, Is.EqualTo("routed"));
    });
  }

  [Test]
  public void Failed_GivenFailure_IsNotSuccessAndCarriesFailure()
  {
    Result<string, int> result = Result<string, int>.Failed(42);

    Assert.Multiple(() =>
    {
      Assert.That(result.IsSuccess, Is.False);
      Assert.That(result.Failure, Is.EqualTo(42));
    });
  }

  [Test]
  public void Value_OnFailedResult_Throws()
  {
    Result<string, int> result = Result<string, int>.Failed(42);

    Assert.Throws<InvalidOperationException>(() => _ = result.Value);
  }

  [Test]
  public void Failure_OnSuccessfulResult_Throws()
  {
    Result<string, int> result = Result<string, int>.Success("routed");

    Assert.Throws<InvalidOperationException>(() => _ = result.Failure);
  }
}
