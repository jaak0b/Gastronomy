using System.Net;
using System.Text.Json;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class HealthEndpointsTest
{

  [SetUp]
  public async Task SetUp()
  {
    factory = await new ApiTestFactory.Builder().StartAsync();
    await using var context = factory.CreateContext();
    await new ApiSeeder().SeedAsync(context, CancellationToken.None);
  }

  [TearDown]
  public async Task TearDown()
  {
    await factory.DisposeAsync();
  }

  private ApiTestFactory factory = null!;

  [Test]
  public async Task GetHealth_AnonymousCaller_ReportsThePrinterCounts()
  {
    using var response = await factory.Client.GetAsync("/api/health");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("status").GetString(), Is.EqualTo("ok"));
                      Assert.That(body.RootElement.GetProperty("printersOnline").GetInt32(), Is.EqualTo(2));
                      Assert.That(body.RootElement.GetProperty("printersTotal").GetInt32(), Is.EqualTo(2));
                    });
  }
}
