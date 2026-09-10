using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminItemCategoryTest
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
  public async Task GetItems_SeededCatalog_CarriesTheCategoryOfEveryArticle()
  {
    var categoryId = await _context.CategoryIdOfAsync("Essen");

    using var response = await _context.Client.GetAsync("/api/admin/items");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var items = body.RootElement.GetProperty("items");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(items[0].GetProperty("categoryId").GetGuid(), Is.EqualTo(categoryId));
                    });
  }

  [Test]
  public async Task PostItem_CategoryThatDoesNotExist_IsRefused()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/items",
                                                              new
                                                              {
                                                                name = "Pommes",
                                                                categoryId = Guid.NewGuid(),
                                                                priceCents = 250,
                                                                sortOrder = 3,
                                                                stationIds = new[] { _context.World.KitchenStationId }
                                                              });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.itemCategoryUnknown"));
                    });
  }

  [Test]
  public async Task PostItem_SwitchedOffCategory_IsRefused()
  {
    var categoryId = await SwitchedOffCategoryIdAsync();

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/items",
                                                              new
                                                              {
                                                                name = "Pommes",
                                                                categoryId,
                                                                priceCents = 250,
                                                                sortOrder = 3,
                                                                stationIds = new[] { _context.World.KitchenStationId }
                                                              });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.itemCategoryIsOff"));
                    });
  }

  [Test]
  public async Task PostItem_CategoryThatIsSwitchedOn_CreatesTheArticleInIt()
  {
    var categoryId = await _context.CategoryIdOfAsync("Essen");

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/items",
                                                              new
                                                              {
                                                                name = "Pommes",
                                                                categoryId,
                                                                priceCents = 250,
                                                                sortOrder = 3,
                                                                stationIds = new[] { _context.World.KitchenStationId }
                                                              });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var itemId = body.RootElement.GetProperty("itemId").GetGuid();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(itemId, Is.Not.EqualTo(Guid.Empty));
                    });
  }

  [Test]
  public async Task PutItem_MovedIntoASwitchedOffCategory_IsRefused()
  {
    var categoryId = await SwitchedOffCategoryIdAsync();

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}",
                                                              new
                                                              {
                                                                name = "Bratwurst mit Brot",
                                                                categoryId,
                                                                priceCents = 350,
                                                                sortOrder = 1,
                                                                stationIds = new[] { _context.World.KitchenStationId }
                                                              });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.itemCategoryIsOff"));
                    });
  }

  [Test]
  public async Task PostItemActivate_ArticleWhoseCategoryIsSwitchedOff_IsRefused()
  {
    var categoryId = await _context.CategoryIdOfAsync("Getraenke");

    using var deactivatedItem =
      await _context.Client.PostAsync($"/api/admin/items/{_context.World.BeerItemId}/deactivate", null);
    Assert.That(deactivatedItem.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    using var deactivatedCategory =
      await _context.Client.PostAsync($"/api/admin/categories/{categoryId}/deactivate", null);
    Assert.That(deactivatedCategory.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    using var response = await _context.Client.PostAsync($"/api/admin/items/{_context.World.BeerItemId}/activate",
                                                        null);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.itemCategoryIsOff"));
                    });
  }

  [Test]
  public async Task PutItem_SwitchedOffArticleInASwitchedOffCategory_IsSaved()
  {
    var categoryId = await _context.CategoryIdOfAsync("Getraenke");
    using var deactivatedItem =
      await _context.Client.PostAsync($"/api/admin/items/{_context.World.BeerItemId}/deactivate", null);
    Assert.That(deactivatedItem.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    using var deactivatedCategory =
      await _context.Client.PostAsync($"/api/admin/categories/{categoryId}/deactivate", null);
    Assert.That(deactivatedCategory.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BeerItemId}",
                                                              new
                                                              {
                                                                name = "Bier vom Fass",
                                                                categoryId,
                                                                sortOrder = 2
                                                              });

    using var items = await _context.Client.GetAsync("/api/admin/items");
    var body = JsonDocument.Parse(await items.Content.ReadAsStringAsync());
    var stored = body.RootElement.GetProperty("items")
                     .EnumerateArray()
                     .First(item => item.GetProperty("itemId").GetGuid() == _context.World.BeerItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.GetProperty("name").GetString(), Is.EqualTo("Bier vom Fass"));
                    });
  }

  [Test]
  public async Task PutItem_ArticleThatDoesNotExist_IsNotFoundRatherThanBlamingTheCategory()
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/items/{Guid.NewGuid()}",
                                                              new
                                                              {
                                                                name = "Pommes",
                                                                categoryId = Guid.NewGuid(),
                                                                priceCents = 250,
                                                                sortOrder = 3,
                                                                stationIds = new[] { _context.World.KitchenStationId }
                                                              });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }

  private async Task<Guid> SwitchedOffCategoryIdAsync()
  {
    using var created = await _context.Client.PostAsJsonAsync("/api/admin/categories",
                                                              new { name = "Kaffee", colourHex = "#6D4C41" });
    Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
    var categoryId = body.RootElement.GetProperty("categoryId").GetGuid();

    using var deactivated = await _context.Client.PostAsync($"/api/admin/categories/{categoryId}/deactivate", null);
    Assert.That(deactivated.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    return categoryId;
  }
}
