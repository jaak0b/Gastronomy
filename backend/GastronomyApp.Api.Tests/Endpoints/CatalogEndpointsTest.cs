using System.Net;
using System.Text.Json;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class CatalogEndpointsTest
{

  [SetUp]
  public async Task SetUp()
  {
    factory = await new ApiTestFactory.Builder().StartAsync();
    await using var context = factory.CreateContext();
    world = await new ApiSeeder().SeedAsync(context, CancellationToken.None);

    using var scope = factory.Services.CreateScope();
    var issued = await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
                            .IssueAsync(world.StaffMemberId, "de", "NUnit", CancellationToken.None);
    deviceToken = issued.PlaintextToken;
  }

  [TearDown]
  public async Task TearDown()
  {
    await factory.DisposeAsync();
  }

  private ApiTestFactory factory = null!;
  private SeededWorld world = null!;
  private string deviceToken = null!;

  [Test]
  public async Task GetCatalog_SeededCatalog_ReturnsTheShapeTheOrderingScreenNeeds()
  {
    var body = await GetCatalogAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(body.RootElement.GetProperty("version").GetString(), Is.Not.Empty);
                      Assert.That(body.RootElement.GetProperty("categories").GetArrayLength(), Is.EqualTo(2));
                      Assert.That(body.RootElement.GetProperty("items").GetArrayLength(), Is.EqualTo(2));
                      Assert.That(body.RootElement.GetProperty("stations").GetArrayLength(), Is.EqualTo(2));
                    });

    var item = body.RootElement.GetProperty("items")[0];

    Assert.Multiple(() =>
                    {
                      Assert.That(item.GetProperty("priceCents").GetInt32(), Is.EqualTo(350));
                      Assert.That(item.GetProperty("stationIds").GetArrayLength(), Is.EqualTo(1));
                      Assert.That(item.GetProperty("isAvailable").GetBoolean(), Is.True);
                    });
  }

  [Test]
  public async Task GetCatalog_SoldOutAndDeactivatedItems_KeepsSoldOutAndLeavesDeactivatedOut()
  {
    await using (var context = factory.CreateContext())
    {
      var bratwurst = await context.CatalogItems.FirstAsync(item => item.Id == world.BratwurstItemId);
      bratwurst.IsAvailable = false;

      var beer = await context.CatalogItems.FirstAsync(item => item.Id == world.BeerItemId);
      beer.IsActive = false;

      await context.SaveChangesAsync();
    }

    var body = await GetCatalogAsync();
    var items = body.RootElement.GetProperty("items");

    Assert.Multiple(() =>
                    {
                      Assert.That(items.GetArrayLength(), Is.EqualTo(1));
                      Assert.That(items[0].GetProperty("id").GetGuid(), Is.EqualTo(world.BratwurstItemId));
                      Assert.That(items[0].GetProperty("isAvailable").GetBoolean(), Is.False);
                    });
  }

  [Test]
  public async Task GetCatalog_NoDeviceToken_IsRefused()
  {
    using var response = await factory.Client.GetAsync("/api/catalog");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  private async Task<JsonDocument> GetCatalogAsync()
  {
    using HttpRequestMessage request = new(HttpMethod.Get, "/api/catalog");
    request.Headers.Authorization = new("Bearer", deviceToken);
    using var response = await factory.Client.SendAsync(request);
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
  }
}
