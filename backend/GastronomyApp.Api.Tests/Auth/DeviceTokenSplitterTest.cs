using GastronomyApp.Api.Auth;

namespace GastronomyApp.Api.Tests.Auth;

[TestFixture]
public sealed class DeviceTokenSplitterTest
{
  private readonly DeviceTokenSplitter _splitter = new();

  [Test]
  public void Split_ALookupIdAndASecret_ReadsBothParts()
  {
    var parts = _splitter.Split("abc123.thesecret");

    Assert.Multiple(() =>
                    {
                      Assert.That(parts!.TokenLookupId, Is.EqualTo("abc123"));
                      Assert.That(parts.Secret, Is.EqualTo("thesecret"));
                    });
  }

  [Test]
  public void Split_ASecretThatContainsTheSeparator_KeepsTheRestOfItInTheSecret()
  {
    var parts = _splitter.Split("abc123.the.secret");

    Assert.Multiple(() =>
                    {
                      Assert.That(parts!.TokenLookupId, Is.EqualTo("abc123"));
                      Assert.That(parts.Secret, Is.EqualTo("the.secret"));
                    });
  }

  [TestCase(null)]
  [TestCase("")]
  [TestCase("nodothere")]
  [TestCase(".secretwithoutalookupid")]
  [TestCase("lookupidwithoutasecret.")]
  public void Split_AnythingNotInThatForm_NamesNoParts(string? presentedToken)
  {
    Assert.That(_splitter.Split(presentedToken), Is.Null);
  }
}
