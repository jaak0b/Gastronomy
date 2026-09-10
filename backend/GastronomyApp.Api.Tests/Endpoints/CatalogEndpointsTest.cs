using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Core.Enums;
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
    _factory = await new ApiTestFactory.Builder().StartAsync();
    await using var context = _factory.CreateContext();
    _world = await new ApiSeeder().SeedAsync(context, CancellationToken.None);

    using var scope = _factory.Services.CreateScope();
    var issued = await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
                            .IssueAsync(new(DeviceOwnerKind.StaffMember, _world.StaffMemberId), "de", "NUnit", CancellationToken.None);
    _deviceToken = issued.PlaintextToken;
  }

  [TearDown]
  public async Task TearDown()
  {
    await _factory.DisposeAsync();
  }

  private ApiTestFactory _factory = null!;
  private SeededWorld _world = null!;
  private string _deviceToken = null!;

  [Test]
  public async Task GetCatalog_SeededCatalog_ReturnsTheShapeTheOrderingScreenNeeds()
  {
    var body = await GetCatalogAsync();

    Assert.Multiple(() =>
                    {
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
                      Assert.That(item.GetProperty("categoryId").GetGuid(), Is.EqualTo(_world.FoodCategoryId));
                    });
  }

  [Test]
  public async Task GetCatalog_SeededCatalog_CarriesEachCategoryWithItsColourAndPosition()
  {
    var body = await GetCatalogAsync();
    var categories = body.RootElement.GetProperty("categories");

    Assert.Multiple(() =>
                    {
                      Assert.That(categories[0].GetProperty("categoryId").GetGuid(), Is.EqualTo(_world.FoodCategoryId));
                      Assert.That(categories[0].GetProperty("name").GetString(), Is.EqualTo("Essen"));
                      Assert.That(categories[0].GetProperty("colourHex").GetString(), Has.Length.EqualTo(7));
                      Assert.That(categories[0].GetProperty("sortOrder").GetInt32(), Is.EqualTo(1));
                      Assert.That(categories[1].GetProperty("categoryId").GetGuid(), Is.EqualTo(_world.DrinkCategoryId));
                      Assert.That(categories[1].GetProperty("sortOrder").GetInt32(), Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task GetCatalog_SwitchedOffCategory_LeavesTheCategoryAndItsArticlesOut()
  {
    using var deactivatedItem =
      await _factory.Client.PostAsync($"/api/admin/items/{_world.BeerItemId}/deactivate", null);
    Assert.That(deactivatedItem.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    using var deactivatedCategory =
      await _factory.Client.PostAsync($"/api/admin/categories/{_world.DrinkCategoryId}/deactivate", null);
    Assert.That(deactivatedCategory.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var body = await GetCatalogAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(body.RootElement.GetProperty("categories").GetArrayLength(), Is.EqualTo(1));
                      Assert.That(body.RootElement.GetProperty("categories")[0].GetProperty("categoryId").GetGuid(),
                                  Is.EqualTo(_world.FoodCategoryId));
                      Assert.That(body.RootElement.GetProperty("items").GetArrayLength(), Is.EqualTo(1));
                      Assert.That(body.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid(),
                                  Is.EqualTo(_world.BratwurstItemId));
                    });
  }

  [Test]
  public async Task GetCatalog_SwitchedOnArticleInASwitchedOffCategory_LeavesThatArticleOut()
  {
    await using (var context = _factory.CreateContext())
    {
      var drinks = await context.CatalogCategories.FirstAsync(category => category.Id == _world.DrinkCategoryId);
      drinks.IsActive = false;
      await context.SaveChangesAsync();
    }

    var body = await GetCatalogAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(body.RootElement.GetProperty("items").GetArrayLength(), Is.EqualTo(1));
                      Assert.That(body.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid(),
                                  Is.EqualTo(_world.BratwurstItemId));
                    });
  }

  [Test]
  public async Task GetCatalog_CategoryWhoseArticlesAreAllSwitchedOff_LeavesTheCategoryOut()
  {
    using var deactivated =
      await _factory.Client.PostAsync($"/api/admin/items/{_world.BeerItemId}/deactivate", null);
    Assert.That(deactivated.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var body = await GetCatalogAsync();
    var categories = body.RootElement.GetProperty("categories");

    Assert.Multiple(() =>
                    {
                      Assert.That(categories.GetArrayLength(), Is.EqualTo(1));
                      Assert.That(categories[0].GetProperty("categoryId").GetGuid(),
                                  Is.EqualTo(_world.FoodCategoryId));
                    });
  }

  [Test]
  public async Task GetCatalog_CategoryWithoutAnyArticles_LeavesTheCategoryOut()
  {
    using var created = await _factory.Client.PostAsJsonAsync("/api/admin/categories",
                                                              new { name = "Kaffee", colourHex = "#6D4C41" });
    Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var body = await GetCatalogAsync();
    var categories = body.RootElement.GetProperty("categories");

    Assert.Multiple(() =>
                    {
                      Assert.That(categories.GetArrayLength(), Is.EqualTo(2));
                      Assert.That(categories[0].GetProperty("categoryId").GetGuid(),
                                  Is.EqualTo(_world.FoodCategoryId));
                      Assert.That(categories[1].GetProperty("categoryId").GetGuid(),
                                  Is.EqualTo(_world.DrinkCategoryId));
                    });
  }

  [Test]
  public async Task GetCatalog_CategoriesMoved_FollowsThePositionsTheLaptopChose()
  {
    using var moved = await _factory.Client.PostAsJsonAsync($"/api/admin/categories/{_world.DrinkCategoryId}/move",
                                                            new { direction = "up" });
    Assert.That(moved.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var body = await GetCatalogAsync();
    var categories = body.RootElement.GetProperty("categories");

    Assert.Multiple(() =>
                    {
                      Assert.That(categories[0].GetProperty("categoryId").GetGuid(), Is.EqualTo(_world.DrinkCategoryId));
                      Assert.That(categories[1].GetProperty("categoryId").GetGuid(), Is.EqualTo(_world.FoodCategoryId));
                    });
  }

  [Test]
  public async Task GetCatalog_SoldOutAndDeactivatedItems_KeepsSoldOutAndLeavesDeactivatedOut()
  {
    await using (var context = _factory.CreateContext())
    {
      var bratwurst = await context.FestivalCatalogItems
                                   .FirstAsync(menuRow => menuRow.CatalogItemId == _world.BratwurstItemId);
      bratwurst.IsAvailable = false;

      var beer = await context.CatalogItems.FirstAsync(item => item.Id == _world.BeerItemId);
      beer.IsActive = false;

      await context.SaveChangesAsync();
    }

    var body = await GetCatalogAsync();
    var items = body.RootElement.GetProperty("items");

    Assert.Multiple(() =>
                    {
                      Assert.That(items.GetArrayLength(), Is.EqualTo(1));
                      Assert.That(items[0].GetProperty("id").GetGuid(), Is.EqualTo(_world.BratwurstItemId));
                      Assert.That(items[0].GetProperty("isAvailable").GetBoolean(), Is.False);
                    });
  }

  [Test]
  public async Task GetCatalog_NoDeviceToken_IsRefused()
  {
    using var response = await _factory.Client.GetAsync("/api/catalog");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  private async Task<JsonDocument> GetCatalogAsync()
  {
    using HttpRequestMessage request = new(HttpMethod.Get, "/api/catalog");
    request.Headers.Authorization = new("Bearer", _deviceToken);
    using var response = await _factory.Client.SendAsync(request);
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
  }
}
