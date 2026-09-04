using System.Net;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class SinglePageAppShellTest
{

  [SetUp]
  public async Task SetUp()
  {
    _factory = await new ApiTestFactory.Builder().StartAsync();
    await using var context = _factory.CreateContext();
    await new ApiSeeder().SeedAsync(context, CancellationToken.None);
  }

  [TearDown]
  public async Task TearDown()
  {
    await _factory.DisposeAsync();
  }

  private ApiTestFactory _factory = null!;

  [TestCase("/")]
  [TestCase("/orders")]
  [TestCase("/admin")]
  public async Task Get_ClientRoutedPath_ServesTheSinglePageAppShell(string path)
  {
    using var response = await _factory.Client.GetAsync(path);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("text/html"));
                    });
  }

  [Test]
  public async Task Get_UnknownApiPath_IsNotAnsweredWithTheShell()
  {
    using var response = await _factory.Client.GetAsync("/api/does-not-exist");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }
}
