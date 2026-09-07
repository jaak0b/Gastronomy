using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.Endpoints;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminCatalogConcurrencyTest
{
  private const int SimultaneousCreates = 8;
  private const int RacedSwitches = 8;
  private const string SmallUmlautA = "ä";
  private const string CapitalUmlautA = "Ä";

  private readonly CatalogCategoryNaming _naming = new();

  private string ColdDrinksName => $"Kaltgetr{SmallUmlautA}nke";

  private string ColdDrinksNameWithTheUmlautInCapitals => $"Kaltgetr{CapitalUmlautA}nke";

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
  public async Task PostCategory_TheSameNameTwiceAtTheSameMoment_RefusesTheSecondOneInWords()
  {
    List<Task<HttpResponseMessage>> attempts = [.. Enumerable.Range(0, SimultaneousCreates).Select(_ => CreateColdDrinksCategoryAsync())];

    HttpResponseMessage[] responses = await Task.WhenAll(attempts);
    List<HttpStatusCode> statuses = [.. responses.Select(response => response.StatusCode)];
    List<string?> messageKeys = [];

    foreach (var response in responses)
    {
      messageKeys.Add(await MessageKeyOfAsync(response));
      response.Dispose();
    }

    await using var database = _context.Factory.CreateContext();
    var normalizedName = _naming.Normalized(ColdDrinksName);
    var stored = await database.CatalogCategories.CountAsync(category => category.NormalizedName == normalizedName);

    Assert.Multiple(() =>
                    {
                      Assert.That(statuses,
                                  Has.None.EqualTo(HttpStatusCode.InternalServerError),
                                  "A name collision must never reach the operator as a crash.");
                      Assert.That(statuses, Has.One.EqualTo(HttpStatusCode.Created));
                      Assert.That(messageKeys.Where(key => key is not null),
                                  Is.All.EqualTo("admin.categoryNameTaken"),
                                  "Every refused attempt must say that the name is taken.");
                      Assert.That(stored, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task CreateCategory_AnotherWriterTookTheSameNameFirst_SaysTheNameIsTakenInsteadOfCrashing()
  {
    using var scope = _context.Factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<GastronomyAppDbContext>();
    dbContext.CatalogCategories.Add(new CatalogCategory
                                    {
                                      Id = Guid.NewGuid(),
                                      Name = ColdDrinksName,
                                      NormalizedName = _naming.Normalized(ColdDrinksName),
                                      ColourHex = "#1565C0",
                                      SortOrder = 9,
                                      IsActive = true
                                    });

    var result = await scope.ServiceProvider.GetRequiredService<AdminCategoryHandler>()
                            .CreateAsync(new SaveCategoryRequest
                                         {
                                           Name = ColdDrinksNameWithTheUmlautInCapitals, ColourHex = "#1565C0"
                                         },
                                         CancellationToken.None);

    var refusal = (JsonHttpResult<ApiError>)result;

    Assert.Multiple(() =>
                    {
                      Assert.That(refusal.StatusCode, Is.EqualTo((int)HttpStatusCode.Conflict));
                      Assert.That(refusal.Value!.MessageKey, Is.EqualTo("admin.categoryNameTaken"));
                    });
  }

  [Test]
  public async Task PostActivateItemAndDeactivateCategory_AtTheSameMoment_NeverSwitchesAnArticleOnUnderASwitchedOffCategory()
  {
    var categoryId = await _context.CategoryIdOfAsync("Getraenke");

    for (var attempt = 0; attempt < RacedSwitches; attempt++)
    {
      await SwitchTheBeerOffAsync();

      Task<HttpResponseMessage> deactivation = SendAsync($"/api/admin/categories/{categoryId}/deactivate");
      Task<HttpResponseMessage> activation = SendAsync($"/api/admin/items/{_context.World.BeerItemId}/activate");

      HttpResponseMessage[] responses = await Task.WhenAll(deactivation, activation);
      List<HttpStatusCode> statuses = [.. responses.Select(response => response.StatusCode)];

      foreach (var response in responses)
      {
        response.Dispose();
      }

      await using (var database = _context.Factory.CreateContext())
      {
        var category = await database.CatalogCategories.FirstAsync(candidate => candidate.Id == categoryId);
        var beer = await database.CatalogItems.FirstAsync(item => item.Id == _context.World.BeerItemId);

        Assert.Multiple(() =>
                        {
                          Assert.That(statuses, Has.None.EqualTo(HttpStatusCode.InternalServerError));
                          Assert.That(category.IsActive || !beer.IsActive,
                                      Is.True,
                                      "A switched-on article may never sit in a switched-off category, or the phone drops it without telling anyone.");
                        });
      }

      await SwitchTheDrinksCategoryOnAsync(categoryId);
    }
  }

  private async Task SwitchTheBeerOffAsync()
  {
    using var response = await SendAsync($"/api/admin/items/{_context.World.BeerItemId}/deactivate");
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
  }

  private async Task SwitchTheDrinksCategoryOnAsync(Guid categoryId)
  {
    using var response = await SendAsync($"/api/admin/categories/{categoryId}/activate");
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
  }

  private async Task<string?> MessageKeyOfAsync(HttpResponseMessage response)
  {
    if (response.IsSuccessStatusCode)
    {
      return null;
    }

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.TryGetProperty("messageKey", out var messageKey) ? messageKey.GetString() : null;
  }

  private Task<HttpResponseMessage> SendAsync(string path)
  {
    return _context.Client.PostAsync(path, null);
  }

  private Task<HttpResponseMessage> CreateColdDrinksCategoryAsync()
  {
    return _context.Client.PostAsJsonAsync("/api/admin/categories",
                                           new { name = ColdDrinksName, colourHex = "#1565C0" });
  }
}
