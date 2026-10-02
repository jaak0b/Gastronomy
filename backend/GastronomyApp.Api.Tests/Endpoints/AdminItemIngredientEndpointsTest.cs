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
                      Assert.That(ingredientIds, Is.EqualTo(new[] { bunId, mustardId }));
                      Assert.That(amounts, Is.EqualTo(new[] { 1d, 25.5d }));
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
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients",
                                                               new
                                                               {
                                                                 name,
                                                                 unit = "gram"
                                                               });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("ingredientId").GetGuid();
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
