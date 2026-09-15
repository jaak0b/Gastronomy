using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class CatalogQueueIndependenceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;

  [Test]
  public async Task GetCatalog_AnItemPreparedIndependently_ReportsTheFlagToThePhone()
  {
    await using (var database = _context.Factory.CreateContext())
    {
      var bratwurst = await database.CatalogItems
                                    .FirstAsync(item => item.Id == _context.World.BratwurstItemId);
      bratwurst.IsQueueIndependent = true;
      await database.SaveChangesAsync();
    }

    using var response = await _context.SendAsync(HttpMethod.Get, "/api/catalog");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var item = body.RootElement
                   .GetProperty("items")
                   .EnumerateArray()
                   .Single(candidate => candidate.GetProperty("id").GetGuid() == _context.World.BratwurstItemId);

    Assert.That(item.GetProperty("isQueueIndependent").GetBoolean(), Is.True);
  }

  [Test]
  public async Task PostItem_PreparedIndependently_RoundTripsThroughTheAdminList()
  {
    Guid itemId;

    using (var created = await _context.Client.PostAsJsonAsync("/api/admin/items",
                                                               new
                                                               {
                                                                 name = "Pommes",
                                                                 categoryId = _context.World.FoodCategoryId,
                                                                 sortOrder = 3,
                                                                 productionMinutes = 6,
                                                                 isQueueIndependent = true
                                                               }))
    {
      Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
      var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
      itemId = body.RootElement.GetProperty("itemId").GetGuid();
    }

    using var response = await _context.Client.GetAsync("/api/admin/items");
    var list = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var item = list.RootElement
                   .GetProperty("items")
                   .EnumerateArray()
                   .Single(candidate => candidate.GetProperty("itemId").GetGuid() == itemId);

    Assert.That(item.GetProperty("isQueueIndependent").GetBoolean(), Is.True);
  }

  [Test]
  public async Task PutItem_PreparedIndependently_ReachesTheAdminListAndTheOrderingCatalog()
  {
    using (var saved = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}",
                                                            new
                                                            {
                                                              name = "Bratwurst mit Brot",
                                                              categoryId = _context.World.FoodCategoryId,
                                                              sortOrder = 1,
                                                              productionMinutes = 7,
                                                              isQueueIndependent = true
                                                            }))
    {
      Assert.That(saved.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var adminResponse = await _context.Client.GetAsync("/api/admin/items");
    var adminBody = JsonDocument.Parse(await adminResponse.Content.ReadAsStringAsync());
    var adminItem = adminBody.RootElement
                             .GetProperty("items")
                             .EnumerateArray()
                             .Single(item => item.GetProperty("itemId").GetGuid() == _context.World.BratwurstItemId);

    using var catalogResponse = await _context.SendAsync(HttpMethod.Get, "/api/catalog");
    var catalogBody = JsonDocument.Parse(await catalogResponse.Content.ReadAsStringAsync());
    var catalogItem = catalogBody.RootElement
                                 .GetProperty("items")
                                 .EnumerateArray()
                                 .Single(item => item.GetProperty("id").GetGuid() == _context.World.BratwurstItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(adminItem.GetProperty("isQueueIndependent").GetBoolean(), Is.True);
                      Assert.That(catalogItem.GetProperty("isQueueIndependent").GetBoolean(), Is.True);
                    });
  }
}
