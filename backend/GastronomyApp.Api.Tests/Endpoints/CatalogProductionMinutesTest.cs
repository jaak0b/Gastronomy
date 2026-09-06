using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class CatalogProductionMinutesTest
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

  [TestCase(-1)]
  [TestCase(601)]
  public async Task PostItem_ADurationOutsideTheAllowedRange_IsRefused(int productionMinutes)
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/items",
                                                              new
                                                              {
                                                                name = "Pommes",
                                                                categoryName = "Essen",
                                                                priceCents = 250,
                                                                sortOrder = 3,
                                                                stationIds = new[] { _context.World.KitchenStationId },
                                                                productionMinutes
                                                              });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("catalog.productionMinutesOutOfRange"));
                    });
  }

  [TestCase(0)]
  [TestCase(600)]
  public async Task PostItem_ADurationAtTheEdgeOfTheAllowedRange_IsStored(int productionMinutes)
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/items",
                                                              new
                                                              {
                                                                name = "Pommes",
                                                                categoryName = "Essen",
                                                                priceCents = 250,
                                                                sortOrder = 3,
                                                                stationIds = new[] { _context.World.KitchenStationId },
                                                                productionMinutes
                                                              });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var itemId = body.RootElement.GetProperty("itemId").GetGuid();

    await using var database = _context.Factory.CreateContext();
    var created = await database.CatalogItems.SingleAsync(item => item.Id == itemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(created.ProductionMinutes, Is.EqualTo(productionMinutes));
                    });
  }

  [Test]
  public async Task PutItem_ADuration_ReachesBothTheAdminListAndTheOrderingCatalog()
  {
    using (var saved = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}",
                                                            new
                                                            {
                                                              name = "Bratwurst mit Brot",
                                                              categoryName = "Essen",
                                                              priceCents = 350,
                                                              sortOrder = 1,
                                                              stationIds = new[] { _context.World.KitchenStationId },
                                                              productionMinutes = 7
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
                      Assert.That(adminItem.GetProperty("productionMinutes").GetInt32(), Is.EqualTo(7));
                      Assert.That(catalogItem.GetProperty("productionMinutes").GetInt32(), Is.EqualTo(7));
                    });
  }

  [Test]
  public async Task GetCatalog_AnItemWithoutAStatedDuration_LeavesTheFieldEmptyRatherThanGuessing()
  {
    using var response = await _context.SendAsync(HttpMethod.Get, "/api/catalog");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var item = body.RootElement
                   .GetProperty("items")
                   .EnumerateArray()
                   .Single(candidate => candidate.GetProperty("id").GetGuid() == _context.World.BeerItemId);

    Assert.That(item.GetProperty("productionMinutes").ValueKind, Is.EqualTo(JsonValueKind.Null));
  }
}
