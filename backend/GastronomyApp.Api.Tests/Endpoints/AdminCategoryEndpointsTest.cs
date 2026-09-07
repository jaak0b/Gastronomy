using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminCategoryEndpointsTest
{
  private const string SmallUmlautA = "ä";
  private const string CapitalUmlautA = "Ä";

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
  public async Task GetCategories_SeededCatalog_ListsThemByTheirPosition()
  {
    using var response = await _context.Client.GetAsync("/api/admin/categories");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var categories = body.RootElement.GetProperty("categories");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(categories.GetArrayLength(), Is.EqualTo(2));
                      Assert.That(categories[0].GetProperty("name").GetString(), Is.EqualTo("Essen"));
                      Assert.That(categories[0].GetProperty("sortOrder").GetInt32(), Is.EqualTo(1));
                      Assert.That(categories[0].GetProperty("isActive").GetBoolean(), Is.True);
                      Assert.That(categories[0].GetProperty("colourHex").GetString(), Has.Length.EqualTo(7));
                      Assert.That(categories[1].GetProperty("name").GetString(), Is.EqualTo("Getraenke"));
                    });
  }

  [Test]
  public async Task PostCategory_NewCategory_TakesTheLastPosition()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/categories",
                                                              new { name = "Kaffee", colourHex = "#6D4C41" });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(body.RootElement.GetProperty("name").GetString(), Is.EqualTo("Kaffee"));
                      Assert.That(body.RootElement.GetProperty("colourHex").GetString(), Is.EqualTo("#6D4C41"));
                      Assert.That(body.RootElement.GetProperty("sortOrder").GetInt32(), Is.EqualTo(3));
                      Assert.That(body.RootElement.GetProperty("isActive").GetBoolean(), Is.True);
                      Assert.That(body.RootElement.GetProperty("categoryId").GetGuid(), Is.Not.EqualTo(Guid.Empty));
                    });
  }

  [Test]
  public async Task PostCategory_BlankName_NamesTheNameAsTheMissingPart()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/categories",
                                                              new { name = "   ", colourHex = "#6D4C41" });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.categoryNameMissing"));
                    });
  }

  [Test]
  public async Task PostCategory_NameAnotherCategoryAlreadyHasInAnotherCasing_IsRefused()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/categories",
                                                              new { name = "essen", colourHex = "#6D4C41" });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.categoryNameTaken"));
                    });
  }

  [Test]
  public async Task PostCategory_NameTypedWithSpacesAroundIt_StoresOnlyTheName()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/categories",
                                                              new { name = " Kaffee ", colourHex = "#6D4C41" });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(body.RootElement.GetProperty("name").GetString(), Is.EqualTo("Kaffee"));
                    });
  }

  [Test]
  public async Task PostCategory_NameThatOnlyDiffersBySpacesAroundIt_IsRefused()
  {
    using var first = await _context.Client.PostAsJsonAsync("/api/admin/categories",
                                                            new { name = "Kaffee", colourHex = "#6D4C41" });
    Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/categories",
                                                              new { name = " Kaffee ", colourHex = "#6D4C41" });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.categoryNameTaken"));
                    });
  }

  [Test]
  public async Task PostCategory_NameAnotherCategoryAlreadyHasWithAnUmlautInAnotherCasing_IsRefused()
  {
    using var first = await _context.Client.PostAsJsonAsync("/api/admin/categories",
                                                            new { name = $"Getr{SmallUmlautA}nke", colourHex = "#1565C0" });
    Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/categories",
                                                              new { name = $"GETR{CapitalUmlautA}NKE", colourHex = "#1565C0" });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.categoryNameTaken"));
                    });
  }

  [Test]
  public async Task PostCategory_NameTypedWithCapitalsInsideIt_KeepsTheCapitalisationTheAdminTyped()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/categories",
                                                              new { name = " KalteGetraenke ", colourHex = "#1565C0" });

    var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    using var listResponse = await _context.Client.GetAsync("/api/admin/categories");
    var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
    var stored = list.RootElement.GetProperty("categories")[2];

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(created.RootElement.GetProperty("name").GetString(), Is.EqualTo("KalteGetraenke"));
                      Assert.That(stored.GetProperty("name").GetString(), Is.EqualTo("KalteGetraenke"));
                    });
  }

  [Test]
  public async Task PostCategory_ColourThatIsNotSixHexDigits_IsRefused()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/categories",
                                                              new { name = "Kaffee", colourHex = "braun" });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.categoryColourInvalid"));
                    });
  }

  [Test]
  public async Task PutCategory_RenamedAndRecoloured_KeepsItsPositionAndStoresBoth()
  {
    var categoryId = await _context.CategoryIdOfAsync("Essen");

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/categories/{categoryId}",
                                                              new { name = "Speisen", colourHex = "#2E7D32" });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("name").GetString(), Is.EqualTo("Speisen"));
                      Assert.That(body.RootElement.GetProperty("colourHex").GetString(), Is.EqualTo("#2E7D32"));
                      Assert.That(body.RootElement.GetProperty("sortOrder").GetInt32(), Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task PutCategory_NameAnotherCategoryAlreadyHas_IsRefused()
  {
    var categoryId = await _context.CategoryIdOfAsync("Essen");

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/categories/{categoryId}",
                                                              new { name = "Getraenke", colourHex = "#2E7D32" });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.categoryNameTaken"));
                    });
  }

  [Test]
  public async Task PutCategory_ItsOwnNameUnchanged_IsAccepted()
  {
    var categoryId = await _context.CategoryIdOfAsync("Essen");

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/categories/{categoryId}",
                                                              new { name = "Essen", colourHex = "#2E7D32" });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
  }

  [Test]
  public async Task PostMove_DownFromTheFirstPosition_SwapsItWithTheOneBelow()
  {
    var categoryId = await _context.CategoryIdOfAsync("Essen");

    using var response = await _context.Client.PostAsJsonAsync($"/api/admin/categories/{categoryId}/move",
                                                              new { direction = "down" });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var categories = body.RootElement.GetProperty("categories");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(categories[0].GetProperty("name").GetString(), Is.EqualTo("Getraenke"));
                      Assert.That(categories[0].GetProperty("sortOrder").GetInt32(), Is.EqualTo(1));
                      Assert.That(categories[1].GetProperty("name").GetString(), Is.EqualTo("Essen"));
                      Assert.That(categories[1].GetProperty("sortOrder").GetInt32(), Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task PostMove_UpFromTheFirstPosition_ChangesNothing()
  {
    var categoryId = await _context.CategoryIdOfAsync("Essen");

    using var response = await _context.Client.PostAsJsonAsync($"/api/admin/categories/{categoryId}/move",
                                                              new { direction = "up" });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var categories = body.RootElement.GetProperty("categories");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(categories[0].GetProperty("name").GetString(), Is.EqualTo("Essen"));
                      Assert.That(categories[1].GetProperty("name").GetString(), Is.EqualTo("Getraenke"));
                    });
  }

  [Test]
  public async Task PostMove_UpFromTheLastPosition_SwapsItWithTheOneAbove()
  {
    var categoryId = await _context.CategoryIdOfAsync("Getraenke");

    using var response = await _context.Client.PostAsJsonAsync($"/api/admin/categories/{categoryId}/move",
                                                              new { direction = "up" });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var categories = body.RootElement.GetProperty("categories");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(categories[0].GetProperty("name").GetString(), Is.EqualTo("Getraenke"));
                      Assert.That(categories[1].GetProperty("name").GetString(), Is.EqualTo("Essen"));
                    });
  }

  [Test]
  public async Task PostMove_DownFromTheLastPosition_ChangesNothing()
  {
    var categoryId = await _context.CategoryIdOfAsync("Getraenke");

    using var response = await _context.Client.PostAsJsonAsync($"/api/admin/categories/{categoryId}/move",
                                                              new { direction = "down" });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var categories = body.RootElement.GetProperty("categories");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(categories[0].GetProperty("name").GetString(), Is.EqualTo("Essen"));
                      Assert.That(categories[1].GetProperty("name").GetString(), Is.EqualTo("Getraenke"));
                    });
  }

  [Test]
  public async Task PostDeactivate_CategoryThatStillHoldsSwitchedOnArticles_IsRefused()
  {
    var categoryId = await _context.CategoryIdOfAsync("Getraenke");

    using var response = await _context.Client.PostAsync($"/api/admin/categories/{categoryId}/deactivate", null);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.categoryHasActiveItems"));
                    });
  }

  [Test]
  public async Task PostDeactivate_CategoryWhoseArticlesAreAllSwitchedOff_SwitchesItOff()
  {
    var categoryId = await _context.CategoryIdOfAsync("Getraenke");
    using var itemResponse =
      await _context.Client.PostAsync($"/api/admin/items/{_context.World.BeerItemId}/deactivate", null);
    Assert.That(itemResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    using var response = await _context.Client.PostAsync($"/api/admin/categories/{categoryId}/deactivate", null);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("isActive").GetBoolean(), Is.False);
                    });
  }

  [Test]
  public async Task PostActivate_SwitchedOffCategory_SwitchesItOn()
  {
    var categoryId = await _context.CategoryIdOfAsync("Getraenke");
    using var itemResponse =
      await _context.Client.PostAsync($"/api/admin/items/{_context.World.BeerItemId}/deactivate", null);
    Assert.That(itemResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    using var deactivated = await _context.Client.PostAsync($"/api/admin/categories/{categoryId}/deactivate", null);
    Assert.That(deactivated.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    using var response = await _context.Client.PostAsync($"/api/admin/categories/{categoryId}/activate", null);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("isActive").GetBoolean(), Is.True);
                    });
  }

  [Test]
  public async Task PostDeactivate_CategoryThatDoesNotExist_IsNotFound()
  {
    using var response = await _context.Client.PostAsync($"/api/admin/categories/{Guid.NewGuid()}/deactivate", null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }
}
