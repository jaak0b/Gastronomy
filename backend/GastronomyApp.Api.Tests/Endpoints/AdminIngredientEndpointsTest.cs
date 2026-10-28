using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminIngredientEndpointsTest
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
  public async Task PostIngredient_ANewName_AnswersTheIngredientAndGivesTheFestivalAnUnlimitedRow()
  {
    var unitId = await _context.FindUnitIdAsync("g");

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients",
                                                               new
                                                               {
                                                                 name = "Senf",
                                                                 unitId
                                                               });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    var ingredientId = body.GetProperty("ingredientId").GetGuid();
    await using var database = _context.Factory.CreateContext();
    var stockRow = await database.FestivalIngredients.SingleAsync(stock => stock.IngredientId == ingredientId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(body.GetProperty("name").GetString(), Is.EqualTo("Senf"));
                      Assert.That(body.GetProperty("unitId").GetGuid(), Is.EqualTo(unitId));
                      Assert.That(body.GetProperty("unitName").GetString(), Is.EqualTo("g"));
                      Assert.That(body.GetProperty("isActive").GetBoolean(), Is.True);
                      Assert.That(stockRow.FestivalId, Is.EqualTo(_context.World.FestivalId));
                      Assert.That(stockRow.AvailableAmount, Is.Null);
                    });
  }

  [Test]
  public async Task PostIngredient_NoName_IsRefusedWithNameMissing()
  {
    var unitId = await _context.FindUnitIdAsync("g");

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients",
                                                               new
                                                               {
                                                                 name = " ",
                                                                 unitId
                                                               });

    await AssertValidationRefusalAsync(response, "errors.admin.ingredients.nameMissing");
  }

  [Test]
  public async Task PostIngredient_ANameAnotherIngredientHoldsInOtherCase_IsRefusedWithNameTaken()
  {
    await CreateIngredientAsync("Senf", "g");

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients",
                                                               new
                                                               {
                                                                 name = "senf",
                                                                 unitId = await _context.FindUnitIdAsync("g")
                                                               });

    await AssertValidationRefusalAsync(response, "errors.admin.ingredients.nameTaken");
  }

  [Test]
  public async Task PostIngredient_TheNameOfADeactivatedIngredientInOtherCase_IsRefusedNamingThatIngredient()
  {
    var mustardId = await CreateIngredientAsync("Senf", "g");
    await DeactivateIngredientAsync(mustardId);

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients",
                                                               new
                                                               {
                                                                 name = "senf",
                                                                 unitId = await _context.FindUnitIdAsync("g")
                                                               });

    await AssertValidationRefusalAsync(response, "errors.admin.ingredients.nameBelongsToDeactivatedIngredient");
    Assert.That(JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("parameters").GetProperty("name").GetString(), Is.EqualTo("Senf"));
  }

  [Test]
  public async Task PostIngredient_NoUnit_IsRefusedWithoutStoringAnything()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients",
                                                               new
                                                               {
                                                                 name = "Senf",
                                                                 unitId = (Guid?)null
                                                               });

    await using var database = _context.Factory.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(await database.Ingredients.CountAsync(), Is.Zero);
                    });
  }

  [Test]
  public async Task PostIngredient_AnUnknownUnit_IsRefusedWithoutStoringAnything()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients",
                                                               new
                                                               {
                                                                 name = "Senf",
                                                                 unitId = Guid.NewGuid()
                                                               });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    await using var database = _context.Factory.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("common.errors.actionFailed"));
                      Assert.That(await database.Ingredients.CountAsync(), Is.Zero);
                    });
  }

  [Test]
  public async Task PostIngredient_ASwitchedOffUnit_IsRefusedWithoutStoringAnything()
  {
    var unitId = await CreateUnitAsync("Portion");
    await DeactivateUnitAsync(unitId);

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients",
                                                               new
                                                               {
                                                                 name = "Senf",
                                                                 unitId
                                                               });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    await using var database = _context.Factory.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("common.errors.actionFailed"));
                      Assert.That(await database.Ingredients.CountAsync(), Is.Zero);
                    });
  }

  [Test]
  public async Task GetIngredients_TwoIngredients_ListsThemByName()
  {
    await CreateIngredientAsync("Senf", "g");
    await CreateIngredientAsync("Apfelsaft", "ml");

    var body = JsonDocument.Parse(await _context.Client.GetStringAsync("/api/admin/ingredients")).RootElement;
    List<string?> names = body.GetProperty("ingredients").EnumerateArray().Select(ingredient => ingredient.GetProperty("name").GetString()).ToList();

    Assert.That(names,
                Is.EqualTo(new[]
                           {
                             "Apfelsaft",
                             "Senf"
                           }));
  }

  [Test]
  public async Task GetIngredients_AnIngredientInTheRecipesOfTwoActiveArticles_NamesBothArticlesSortedByName()
  {
    var mustardId = await CreateIngredientAsync("Senf", "g");
    await PutRecipeLineAsync(_context.World.BratwurstItemId, mustardId);
    await PutRecipeLineAsync(_context.World.BeerItemId, mustardId);

    var listed = JsonDocument.Parse(await _context.Client.GetStringAsync("/api/admin/ingredients")).RootElement.GetProperty("ingredients").EnumerateArray().Single(ingredient => ingredient.GetProperty("ingredientId").GetGuid() == mustardId);
    List<string?> activeItemNames = listed.GetProperty("activeItemNames").EnumerateArray().Select(itemName => itemName.GetString()).ToList();

    Assert.That(activeItemNames,
                Is.EqualTo(new[]
                           {
                             "Bier",
                             "Bratwurst mit Brot"
                           }));
  }

  [Test]
  public async Task GetIngredients_AnIngredientAlsoInTheRecipeOfASwitchedOffArticle_LeavesThatArticleOut()
  {
    var mustardId = await CreateIngredientAsync("Senf", "g");
    await PutRecipeLineAsync(_context.World.BratwurstItemId, mustardId);
    await PutRecipeLineAsync(_context.World.BeerItemId, mustardId);
    await SwitchTheBeerOffAsync();

    var listed = JsonDocument.Parse(await _context.Client.GetStringAsync("/api/admin/ingredients")).RootElement.GetProperty("ingredients").EnumerateArray().Single(ingredient => ingredient.GetProperty("ingredientId").GetGuid() == mustardId);
    List<string?> activeItemNames = listed.GetProperty("activeItemNames").EnumerateArray().Select(itemName => itemName.GetString()).ToList();

    Assert.That(activeItemNames, Is.EqualTo(new[] { "Bratwurst mit Brot" }));
  }

  [Test]
  public async Task PutIngredient_ANewNameAndUnit_AnswersTheIdAndStoresBoth()
  {
    var ingredientId = await CreateIngredientAsync("Senf", "g");
    var millilitreUnitId = await _context.FindUnitIdAsync("ml");

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/ingredients/{ingredientId}",
                                                              new
                                                              {
                                                                name = "Senf mittelscharf",
                                                                unitId = millilitreUnitId
                                                              });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    var listed = JsonDocument.Parse(await _context.Client.GetStringAsync("/api/admin/ingredients")).RootElement.GetProperty("ingredients")[0];

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.GetProperty("ingredientId").GetGuid(), Is.EqualTo(ingredientId));
                      Assert.That(listed.GetProperty("name").GetString(), Is.EqualTo("Senf mittelscharf"));
                      Assert.That(listed.GetProperty("unitId").GetGuid(), Is.EqualTo(millilitreUnitId));
                      Assert.That(listed.GetProperty("unitName").GetString(), Is.EqualTo("ml"));
                    });
  }

  [Test]
  public async Task PutIngredient_ANewUnit_ChangesTheUnitAndKeepsTheRecipeAmountAndTheStockAmount()
  {
    var ingredientId = await CreateIngredientAsync("Senf", "g");
    await PutRecipeLineAsync(_context.World.BratwurstItemId, ingredientId, 25.5);
    await SetAvailableAmountAsync(ingredientId, 500);

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/ingredients/{ingredientId}",
                                                              new
                                                              {
                                                                name = "Senf",
                                                                unitId = await _context.FindUnitIdAsync("ml")
                                                              });

    var listed = JsonDocument.Parse(await _context.Client.GetStringAsync($"/api/admin/items?festivalId={_context.World.FestivalId}"));
    var line = listed.RootElement.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("itemId").GetGuid() == _context.World.BratwurstItemId).GetProperty("ingredients").EnumerateArray().Single();
    await using var database = _context.Factory.CreateContext();
    var stock = await database.FestivalIngredients.SingleAsync(row => row.IngredientId == ingredientId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(line.GetProperty("unitName").GetString(), Is.EqualTo("ml"));
                      Assert.That(line.GetProperty("amount").GetDouble(), Is.EqualTo(25.5));
                      Assert.That(stock.AvailableAmount, Is.EqualTo(500));
                    });
  }

  [Test]
  public async Task PutIngredient_ANameAnotherIngredientHolds_IsRefusedWithNameTaken()
  {
    await CreateIngredientAsync("Senf", "g");
    var ketchupId = await CreateIngredientAsync("Ketchup", "g");

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/ingredients/{ketchupId}",
                                                              new
                                                              {
                                                                name = "SENF",
                                                                unitId = await _context.FindUnitIdAsync("g")
                                                              });

    await AssertValidationRefusalAsync(response, "errors.admin.ingredients.nameTaken");
  }

  [Test]
  public async Task PutIngredient_TheNameOfADeactivatedIngredient_IsRefusedNamingThatIngredient()
  {
    var mustardId = await CreateIngredientAsync("Senf", "g");
    await DeactivateIngredientAsync(mustardId);
    var ketchupId = await CreateIngredientAsync("Ketchup", "g");

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/ingredients/{ketchupId}",
                                                              new
                                                              {
                                                                name = "SENF",
                                                                unitId = await _context.FindUnitIdAsync("g")
                                                              });

    await AssertValidationRefusalAsync(response, "errors.admin.ingredients.nameBelongsToDeactivatedIngredient");
    Assert.That(JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("parameters").GetProperty("name").GetString(), Is.EqualTo("Senf"));
  }

  [Test]
  public async Task PutIngredient_AnUnknownIngredient_AnswersNotFound()
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/ingredients/{Guid.NewGuid()}",
                                                              new
                                                              {
                                                                name = "Senf",
                                                                unitId = await _context.FindUnitIdAsync("g")
                                                              });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }

  [Test]
  public async Task PostDeactivateThenActivate_AnIngredient_SwitchesItOffAndOnAgain()
  {
    var ingredientId = await CreateIngredientAsync("Senf", "g");

    using var deactivated = await _context.Client.PostAsync($"/api/admin/ingredients/{ingredientId}/deactivate", null);
    var afterDeactivate = JsonDocument.Parse(await _context.Client.GetStringAsync("/api/admin/ingredients")).RootElement.GetProperty("ingredients")[0].GetProperty("isActive").GetBoolean();
    using var activated = await _context.Client.PostAsync($"/api/admin/ingredients/{ingredientId}/activate", null);
    var afterActivate = JsonDocument.Parse(await _context.Client.GetStringAsync("/api/admin/ingredients")).RootElement.GetProperty("ingredients")[0].GetProperty("isActive").GetBoolean();

    Assert.Multiple(() =>
                    {
                      Assert.That(deactivated.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(activated.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(afterDeactivate, Is.False);
                      Assert.That(afterActivate, Is.True);
                    });
  }

  [Test]
  public async Task PostDeactivate_AnIngredientInTheRecipeOfAnActiveArticle_IsRefusedAndStaysActive()
  {
    var mustardId = await CreateIngredientAsync("Senf", "g");
    await PutRecipeLineAsync(_context.World.BratwurstItemId, mustardId);

    using var response = await _context.Client.PostAsync($"/api/admin/ingredients/{mustardId}/deactivate", null);

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    await using var database = _context.Factory.CreateContext();
    var stored = await database.Ingredients.SingleAsync(ingredient => ingredient.Id == mustardId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("IngredientUsedByActiveItems"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("common.errors.actionFailed"));
                      Assert.That(stored.IsActive, Is.True);
                    });
  }

  private async Task<Guid> CreateIngredientAsync(string name, string unitName)
  {
    var unitId = await _context.FindUnitIdAsync(unitName);

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients",
                                                               new
                                                               {
                                                                 name,
                                                                 unitId
                                                               });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("ingredientId").GetGuid();
  }

  private async Task<Guid> CreateUnitAsync(string name)
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/units", new { name });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("unitId").GetGuid();
  }

  private async Task DeactivateUnitAsync(Guid unitId)
  {
    using var response = await _context.Client.PostAsync($"/api/admin/units/{unitId}/deactivate", null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
  }

  private async Task DeactivateIngredientAsync(Guid ingredientId)
  {
    using var response = await _context.Client.PostAsync($"/api/admin/ingredients/{ingredientId}/deactivate", null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
  }

  private async Task PutRecipeLineAsync(Guid itemId, Guid ingredientId, double amount = 20)
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/items/{itemId}/ingredients/{ingredientId}", new { amount });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
  }

  private async Task SetAvailableAmountAsync(Guid ingredientId, double availableAmount)
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/ingredients/{ingredientId}", new { availableAmount });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
  }

  private async Task SwitchTheBeerOffAsync()
  {
    await using var database = _context.Factory.CreateContext();
    var beer = await database.CatalogItems.FirstAsync(item => item.Id == _context.World.BeerItemId);
    beer.IsActive = false;
    await database.SaveChangesAsync();
  }

  private async Task AssertValidationRefusalAsync(HttpResponseMessage response, string messageKey)
  {
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("ValidationFailed"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo(messageKey));
                    });
  }
}
