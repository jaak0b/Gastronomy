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
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients",
                                                               new
                                                               {
                                                                 name = "Senf",
                                                                 unit = "gram"
                                                               });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    var ingredientId = body.GetProperty("ingredientId").GetGuid();
    await using var database = _context.Factory.CreateContext();
    var stockRow = await database.FestivalIngredients.SingleAsync(stock => stock.IngredientId == ingredientId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(body.GetProperty("name").GetString(), Is.EqualTo("Senf"));
                      Assert.That(body.GetProperty("unit").GetString(), Is.EqualTo("gram"));
                      Assert.That(body.GetProperty("isActive").GetBoolean(), Is.True);
                      Assert.That(stockRow.FestivalId, Is.EqualTo(_context.World.FestivalId));
                      Assert.That(stockRow.AvailableAmount, Is.Null);
                    });
  }

  [Test]
  public async Task PostIngredient_NoName_IsRefusedWithNameMissing()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients",
                                                               new
                                                               {
                                                                 name = " ",
                                                                 unit = "piece"
                                                               });

    await AssertValidationRefusalAsync(response, "errors.admin.ingredients.nameMissing");
  }

  [Test]
  public async Task PostIngredient_ANameAnotherIngredientHoldsInOtherCase_IsRefusedWithNameTaken()
  {
    await CreateIngredientAsync("Senf", "gram");

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients",
                                                               new
                                                               {
                                                                 name = "senf",
                                                                 unit = "gram"
                                                               });

    await AssertValidationRefusalAsync(response, "errors.admin.ingredients.nameTaken");
  }

  [Test]
  public async Task PostIngredient_NoUnit_IsRefusedWithoutStoringAnything()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients", new { name = "Senf", unit = (string?)null });

    await using var database = _context.Factory.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(await database.Ingredients.CountAsync(), Is.Zero);
                    });
  }

  [Test]
  public async Task GetIngredients_TwoIngredients_ListsThemByName()
  {
    await CreateIngredientAsync("Senf", "gram");
    await CreateIngredientAsync("Apfelsaft", "millilitre");

    var body = JsonDocument.Parse(await _context.Client.GetStringAsync("/api/admin/ingredients")).RootElement;
    List<string?> names = body.GetProperty("ingredients").EnumerateArray().Select(ingredient => ingredient.GetProperty("name").GetString()).ToList();

    Assert.That(names, Is.EqualTo(new[] { "Apfelsaft", "Senf" }));
  }

  [Test]
  public async Task PutIngredient_ANewNameAndUnit_AnswersTheIdAndStoresBoth()
  {
    var ingredientId = await CreateIngredientAsync("Senf", "gram");

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/ingredients/{ingredientId}",
                                                              new
                                                              {
                                                                name = "Senf mittelscharf",
                                                                unit = "millilitre"
                                                              });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    var listed = JsonDocument.Parse(await _context.Client.GetStringAsync("/api/admin/ingredients")).RootElement.GetProperty("ingredients")[0];

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.GetProperty("ingredientId").GetGuid(), Is.EqualTo(ingredientId));
                      Assert.That(listed.GetProperty("name").GetString(), Is.EqualTo("Senf mittelscharf"));
                      Assert.That(listed.GetProperty("unit").GetString(), Is.EqualTo("millilitre"));
                    });
  }

  [Test]
  public async Task PutIngredient_ANameAnotherIngredientHolds_IsRefusedWithNameTaken()
  {
    await CreateIngredientAsync("Senf", "gram");
    var ketchupId = await CreateIngredientAsync("Ketchup", "gram");

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/ingredients/{ketchupId}",
                                                              new
                                                              {
                                                                name = "SENF",
                                                                unit = "gram"
                                                              });

    await AssertValidationRefusalAsync(response, "errors.admin.ingredients.nameTaken");
  }

  [Test]
  public async Task PutIngredient_AnUnknownIngredient_AnswersNotFound()
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/ingredients/{Guid.NewGuid()}",
                                                              new
                                                              {
                                                                name = "Senf",
                                                                unit = "gram"
                                                              });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }

  [Test]
  public async Task PostDeactivateThenActivate_AnIngredient_SwitchesItOffAndOnAgain()
  {
    var ingredientId = await CreateIngredientAsync("Senf", "gram");

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

  private async Task<Guid> CreateIngredientAsync(string name, string unit)
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients",
                                                               new
                                                               {
                                                                 name,
                                                                 unit
                                                               });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("ingredientId").GetGuid();
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
