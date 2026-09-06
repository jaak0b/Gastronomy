using System.Net;
using System.Text.Json;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class HealthEndpointsTest
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

  [Test]
  public async Task GetHealth_AnonymousCaller_ReportsTheStationCounts()
  {
    using var response = await _factory.Client.GetAsync("/api/health");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("status").GetString(), Is.EqualTo("ok"));
                      Assert.That(body.RootElement.GetProperty("activeStationCount").GetInt32(), Is.EqualTo(2));
                      Assert.That(body.RootElement.GetProperty("stationsWithADeviceCount").GetInt32(), Is.Zero);
                    });
  }
}
