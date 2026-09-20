using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminItemNameTakenTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContextBuilder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;

  [Test]
  public async Task PostItem_NameThatAlreadyExists_IsRefused()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/items",
                                                              new
                                                              {
                                                                name = "Bratwurst mit Brot",
                                                                categoryId = _context.World.FoodCategoryId,
                                                                sortOrder = 3
                                                              });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.itemNameTaken"));
                    });
  }

  [Test]
  public async Task PostItem_NameOfASwitchedOffArticle_IsStillRefused()
  {
    await using (var database = _context.Factory.CreateContext())
    {
      var beer = await database.CatalogItems.FirstAsync(item => item.Id == _context.World.BeerItemId);
      beer.IsActive = false;
      await database.SaveChangesAsync();
    }

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/items",
                                                              new
                                                              {
                                                                name = "Bier",
                                                                categoryId = _context.World.DrinkCategoryId,
                                                                sortOrder = 3
                                                              });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.itemNameTaken"));
                    });
  }

  [Test]
  public async Task PutItem_RenamedOntoAnotherArticlesName_IsRefused()
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}",
                                                              new
                                                              {
                                                                name = "Bier",
                                                                categoryId = _context.World.FoodCategoryId,
                                                                sortOrder = 1
                                                              });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.itemNameTaken"));
                    });
  }

  [Test]
  public async Task PutItem_KeepingItsOwnName_IsSaved()
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}",
                                                              new
                                                              {
                                                                name = "Bratwurst mit Brot",
                                                                categoryId = _context.World.FoodCategoryId,
                                                                sortOrder = 1
                                                              });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
  }
}
