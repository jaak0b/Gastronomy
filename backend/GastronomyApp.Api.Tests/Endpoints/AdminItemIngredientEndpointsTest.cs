using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminItemIngredientEndpointsTest
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
  public async Task PutItemIngredient_TwoIngredients_ListsTheArticleWithItsRecipeByIngredientName()
  {
    var mustardId = await CreateIngredientAsync("Senf");
    var bunId = await CreateIngredientAsync("Brötchen");

    using var mustard = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}/ingredients/{mustardId}", new { amount = 20 });
    using var bun = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}/ingredients/{bunId}", new { amount = 1 });
    using var changed = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}/ingredients/{mustardId}", new { amount = 25.5 });

    var saved = JsonDocument.Parse(await changed.Content.ReadAsStringAsync()).RootElement;
    var bratwurst = await FindListedBratwurstAsync();
    List<Guid> ingredientIds = bratwurst.GetProperty("ingredients").EnumerateArray().Select(line => line.GetProperty("ingredientId").GetGuid()).ToList();
    List<double> amounts = bratwurst.GetProperty("ingredients").EnumerateArray().Select(line => line.GetProperty("amount").GetDouble()).ToList();

    Assert.Multiple(() =>
                    {
                      Assert.That(mustard.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(bun.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(changed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(saved.GetProperty("itemId").GetGuid(), Is.EqualTo(_context.World.BratwurstItemId));
                      Assert.That(ingredientIds,
                                  Is.EqualTo(new[]
                                             {
                                               bunId,
                                               mustardId
                                             }));
                      Assert.That(amounts,
                                  Is.EqualTo(new[]
                                             {
                                               1d,
                                               25.5d
                                             }));
                    });
  }

  [Test]
  public async Task GetItems_ARecipeOfTwoIngredients_CarriesTheNameAndUnitOfEveryLine()
  {
    var mustardId = await CreateIngredientAsync("Senf", "g");
    var bunId = await CreateIngredientAsync("Brötchen", "Stück");
    await PutRecipeLineAsync(mustardId, 20);
    await PutRecipeLineAsync(bunId, 1);

    var bratwurst = await FindListedBratwurstAsync();

    List<string?> names = bratwurst.GetProperty("ingredients").EnumerateArray().Select(line => line.GetProperty("name").GetString()).ToList();
    List<string?> units = bratwurst.GetProperty("ingredients").EnumerateArray().Select(line => line.GetProperty("unitName").GetString()).ToList();

    Assert.Multiple(() =>
                    {
                      Assert.That(names,
                                  Is.EqualTo(new[]
                                             {
                                               "Brötchen",
                                               "Senf"
                                             }));
                      Assert.That(units,
                                  Is.EqualTo(new[]
                                             {
                                               "Stück",
                                               "g"
                                             }));
                    });
  }

  [Test]
  public async Task GetItems_AnIngredientRenamedAfterJoiningTheRecipe_CarriesTheNewName()
  {
    var mustardId = await CreateIngredientAsync("Senf");
    await PutRecipeLineAsync(mustardId, 20);
    await RenameIngredientAsync(mustardId, "Dijonsenf");

    var bratwurst = await FindListedBratwurstAsync();

    var line = bratwurst.GetProperty("ingredients").EnumerateArray().Single();
    Assert.That(line.GetProperty("name").GetString(), Is.EqualTo("Dijonsenf"));
  }

  [Test]
  public async Task GetItems_AnIngredientWhoseUnitChanged_CarriesTheNewUnitAndTheSameAmount()
  {
    var mustardId = await CreateIngredientAsync("Senf");
    await PutRecipeLineAsync(mustardId, 20);

    using (var changed = await _context.Client.PutAsJsonAsync($"/api/admin/ingredients/{mustardId}",
                                                              new
                                                              {
                                                                name = "Senf",
                                                                unitId = await _context.FindUnitIdAsync("ml")
                                                              }))
    {
      Assert.That(changed.StatusCode, Is.EqualTo(HttpStatusCode.OK), await changed.Content.ReadAsStringAsync());
    }

    var bratwurst = await FindListedBratwurstAsync();
    var line = bratwurst.GetProperty("ingredients").EnumerateArray().Single();

    Assert.Multiple(() =>
                    {
                      Assert.That(line.GetProperty("unitName").GetString(), Is.EqualTo("ml"));
                      Assert.That(line.GetProperty("amount").GetDouble(), Is.EqualTo(20));
                    });
  }

  [TestCase(0d)]
  [TestCase(-3d)]
  public async Task PutItemIngredient_AnAmountNotAboveZero_IsRefusedWithAmountInvalid(double amount)
  {
    var mustardId = await CreateIngredientAsync("Senf");

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}/ingredients/{mustardId}", new { amount });

    await AssertAmountInvalidAsync(response);
  }

  [Test]
  public async Task PutItemIngredient_NoAmount_IsRefusedWithAmountInvalid()
  {
    var mustardId = await CreateIngredientAsync("Senf");

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}/ingredients/{mustardId}", new { amount = (double?)null });

    await AssertAmountInvalidAsync(response);
  }

  [Test]
  public async Task PutItemIngredient_AnUnknownIngredient_AnswersNotFound()
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}/ingredients/{Guid.NewGuid()}", new { amount = 1 });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }

  [Test]
  public async Task PutItemIngredient_ADeactivatedIngredientNotInTheRecipe_IsRefusedWithoutAddingIt()
  {
    var onionsId = await CreateIngredientAsync("Zwiebeln");
    await DeactivateIngredientAsync(onionsId);

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}/ingredients/{onionsId}", new { amount = 15 });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    await using var database = _context.Factory.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("DeactivatedIngredientCannotJoinRecipe"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(await database.CatalogItemIngredients.CountAsync(), Is.Zero);
                    });
  }

  [Test]
  public async Task PutItemIngredient_ADeactivatedIngredientAlreadyInTheRecipe_ChangesTheAmount()
  {
    var onionsId = await CreateIngredientAsync("Zwiebeln");
    await PutRecipeLineAsync(onionsId, 15);
    await SwitchTheBratwurstOffAsync();
    await DeactivateIngredientAsync(onionsId);

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}/ingredients/{onionsId}", new { amount = 25 });

    await using var database = _context.Factory.CreateContext();
    var stored = await database.CatalogItemIngredients.SingleAsync(recipeLine => recipeLine.IngredientId == onionsId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.Amount, Is.EqualTo(25));
                    });
  }

  [Test]
  public async Task PostItemActivate_ARecipeWithADeactivatedIngredient_IsRefusedNamingTheIngredient()
  {
    var mustardId = await CreateIngredientAsync("Senf");
    var onionsId = await CreateIngredientAsync("Zwiebeln");
    await PutRecipeLineAsync(mustardId, 20);
    await PutRecipeLineAsync(onionsId, 15);
    await SwitchTheBratwurstOffAsync();
    await DeactivateIngredientAsync(onionsId);

    using var response = await _context.Client.PostAsync($"/api/admin/items/{_context.World.BratwurstItemId}/activate", null);

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    await using var database = _context.Factory.CreateContext();
    var bratwurst = await database.CatalogItems.SingleAsync(item => item.Id == _context.World.BratwurstItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.items.ingredientIsDeactivated"));
                      Assert.That(body.GetProperty("parameters").GetProperty("name").GetString(), Is.EqualTo("Zwiebeln"));
                      Assert.That(bratwurst.IsActive, Is.False);
                    });
  }

  [Test]
  public async Task DeleteItemIngredient_AnExistingLine_AnswersNoContentAndRemovesIt()
  {
    var mustardId = await CreateIngredientAsync("Senf");
    using (var put = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}/ingredients/{mustardId}", new { amount = 20 }))
    {
      Assert.That(put.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await _context.Client.DeleteAsync($"/api/admin/items/{_context.World.BratwurstItemId}/ingredients/{mustardId}");

    await using var database = _context.Factory.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
                      Assert.That(await database.CatalogItemIngredients.CountAsync(), Is.Zero);
                    });
  }

  [Test]
  public async Task DeleteItemIngredient_NoSuchLine_AnswersNotFound()
  {
    using var response = await _context.Client.DeleteAsync($"/api/admin/items/{_context.World.BratwurstItemId}/ingredients/{Guid.NewGuid()}");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }

  private async Task<JsonElement> FindListedBratwurstAsync()
  {
    var listed = JsonDocument.Parse(await _context.Client.GetStringAsync($"/api/admin/items?festivalId={_context.World.FestivalId}")).RootElement;

    return listed.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("itemId").GetGuid() == _context.World.BratwurstItemId);
  }

  private async Task<Guid> CreateIngredientAsync(string name)
  {
    return await CreateIngredientAsync(name, "g");
  }

  private async Task<Guid> CreateIngredientAsync(string name, string unitName)
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients",
                                                               new
                                                               {
                                                                 name,
                                                                 unitId = await _context.FindUnitIdAsync(unitName)
                                                               });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("ingredientId").GetGuid();
  }

  private async Task RenameIngredientAsync(Guid ingredientId, string name)
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/ingredients/{ingredientId}",
                                                              new
                                                              {
                                                                name,
                                                                unitId = await _context.FindUnitIdAsync("g")
                                                              });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
  }

  private async Task PutRecipeLineAsync(Guid ingredientId, double amount)
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}/ingredients/{ingredientId}", new { amount });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
  }

  private async Task DeactivateIngredientAsync(Guid ingredientId)
  {
    using var response = await _context.Client.PostAsync($"/api/admin/ingredients/{ingredientId}/deactivate", null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
  }

  private async Task SwitchTheBratwurstOffAsync()
  {
    await using var database = _context.Factory.CreateContext();
    var bratwurst = await database.CatalogItems.FirstAsync(item => item.Id == _context.World.BratwurstItemId);
    bratwurst.IsActive = false;
    await database.SaveChangesAsync();
  }

  private async Task AssertAmountInvalidAsync(HttpResponseMessage response)
  {
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("ValidationFailed"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.ingredients.amountInvalid"));
                    });
  }
}
