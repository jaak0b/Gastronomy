using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminFestivalIngredientEndpointsTest
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
  public async Task GetFestivalIngredients_OneIngredientInARecipeAndOneNot_ListsOnlyTheUsedOneWithWhatTheOrdersUsed()
  {
    var mustardId = await CreateIngredientAsync("Senf");
    await CreateIngredientAsync("Zwiebeln");
    await PutRecipeLineAsync(mustardId, 20);
    await SetAvailableAmountAsync(mustardId, 1000);
    using (var ordered = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(ordered.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    using var response = await _context.Client.GetAsync($"/api/admin/festivals/{_context.World.FestivalId}/ingredients");
    var ingredients = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("ingredients");
    var mustard = ingredients[0];

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(ingredients.GetArrayLength(), Is.EqualTo(1));
                      Assert.That(mustard.GetProperty("ingredientId").GetGuid(), Is.EqualTo(mustardId));
                      Assert.That(mustard.GetProperty("name").GetString(), Is.EqualTo("Senf"));
                      Assert.That(mustard.GetProperty("unit").GetString(), Is.EqualTo("gram"));
                      Assert.That(mustard.GetProperty("isActive").GetBoolean(), Is.True);
                      Assert.That(mustard.GetProperty("availableAmount").GetDouble(), Is.EqualTo(1000));
                      Assert.That(mustard.GetProperty("usedAmount").GetDouble(), Is.EqualTo(40));
                      Assert.That(mustard.GetProperty("runsOutAtUtc").GetString(), Does.EndWith("Z"));
                      Assert.That(mustard.GetProperty("runsOutAtUtc").GetDateTime(), Is.GreaterThan(DateTime.UtcNow));
                    });
  }

  [Test]
  public async Task GetFestivalIngredients_NoLimitSet_AnswersNoAvailableAmountAndNoTime()
  {
    var mustardId = await CreateIngredientAsync("Senf");
    await PutRecipeLineAsync(mustardId, 20);

    var mustard = JsonDocument.Parse(await _context.Client.GetStringAsync($"/api/admin/festivals/{_context.World.FestivalId}/ingredients")).RootElement.GetProperty("ingredients")[0];

    Assert.Multiple(() =>
                    {
                      Assert.That(mustard.GetProperty("availableAmount").ValueKind, Is.EqualTo(JsonValueKind.Null));
                      Assert.That(mustard.GetProperty("usedAmount").GetDouble(), Is.Zero);
                      Assert.That(mustard.GetProperty("runsOutAtUtc").ValueKind, Is.EqualTo(JsonValueKind.Null));
                    });
  }

  [Test]
  public async Task GetFestivalIngredients_AnUnknownFestival_AnswersNotFound()
  {
    using var response = await _context.Client.GetAsync($"/api/admin/festivals/{Guid.NewGuid()}/ingredients");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }

  [Test]
  public async Task PutFestivalIngredient_AnAmount_AnswersTheIdAndStoresIt()
  {
    var mustardId = await CreateIngredientAsync("Senf");

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/ingredients/{mustardId}", new { availableAmount = 750.5 });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    await using var database = _context.Factory.CreateContext();
    var stored = await database.FestivalIngredients.SingleAsync(stock => stock.IngredientId == mustardId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.GetProperty("ingredientId").GetGuid(), Is.EqualTo(mustardId));
                      Assert.That(stored.AvailableAmount, Is.EqualTo(750.5));
                    });
  }

  [Test]
  public async Task PutFestivalIngredient_NoAmount_StoresUnlimited()
  {
    var mustardId = await CreateIngredientAsync("Senf");
    await SetAvailableAmountAsync(mustardId, 10);

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/ingredients/{mustardId}", new { availableAmount = (double?)null });

    await using var database = _context.Factory.CreateContext();
    var stored = await database.FestivalIngredients.SingleAsync(stock => stock.IngredientId == mustardId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.AvailableAmount, Is.Null);
                    });
  }

  [Test]
  public async Task PutFestivalIngredient_AnAmountBelowZero_IsRefusedWithStockInvalid()
  {
    var mustardId = await CreateIngredientAsync("Senf");

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/ingredients/{mustardId}", new { availableAmount = -1 });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("ValidationFailed"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.ingredients.stockInvalid"));
                    });
  }

  [Test]
  public async Task PutFestivalIngredient_LessThanOnePortion_MarksTheArticleSoldOut()
  {
    var mustardId = await CreateIngredientAsync("Senf");
    await PutRecipeLineAsync(mustardId, 20);

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/ingredients/{mustardId}", new { availableAmount = 19 });

    await using var database = _context.Factory.CreateContext();
    var bratwurstRow = await database.FestivalCatalogItems.SingleAsync(menuRow => menuRow.FestivalId == _context.World.FestivalId && menuRow.CatalogItemId == _context.World.BratwurstItemId);
    var beerRow = await database.FestivalCatalogItems.SingleAsync(menuRow => menuRow.FestivalId == _context.World.FestivalId && menuRow.CatalogItemId == _context.World.BeerItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(bratwurstRow.IsAvailable, Is.False);
                      Assert.That(beerRow.IsAvailable, Is.True);
                    });
  }

  [Test]
  public async Task PostOrder_TheOrderUsesUpTheStock_AcceptsTheOrderAndMarksTheArticleSoldOut()
  {
    var mustardId = await CreateIngredientAsync("Senf");
    await PutRecipeLineAsync(mustardId, 20);
    await SetAvailableAmountAsync(mustardId, 50);

    using var ordered = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));

    await using var database = _context.Factory.CreateContext();
    var bratwurstRow = await database.FestivalCatalogItems.SingleAsync(menuRow => menuRow.FestivalId == _context.World.FestivalId && menuRow.CatalogItemId == _context.World.BratwurstItemId);

    Assert.Multiple(async () =>
                    {
                      Assert.That(ordered.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(await database.Orders.CountAsync(), Is.EqualTo(1));
                      Assert.That(bratwurstRow.IsAvailable, Is.False);
                    });
  }

  private async Task<Guid> CreateIngredientAsync(string name)
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients",
                                                               new
                                                               {
                                                                 name,
                                                                 unit = "gram"
                                                               });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("ingredientId").GetGuid();
  }

  private async Task PutRecipeLineAsync(Guid ingredientId, double amount)
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/items/{_context.World.BratwurstItemId}/ingredients/{ingredientId}", new { amount });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
  }

  private async Task SetAvailableAmountAsync(Guid ingredientId, double availableAmount)
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/ingredients/{ingredientId}", new { availableAmount });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
  }
}
