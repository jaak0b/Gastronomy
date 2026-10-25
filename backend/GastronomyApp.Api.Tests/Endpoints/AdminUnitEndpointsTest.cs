using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminUnitEndpointsTest
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
  public async Task PostUnit_ANewName_AnswersTheUnitAndListsIt()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/units", new { name = "Portion" });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    var unitId = body.GetProperty("unitId").GetGuid();
    var listed = await FindListedUnitAsync(unitId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(body.GetProperty("name").GetString(), Is.EqualTo("Portion"));
                      Assert.That(body.GetProperty("isActive").GetBoolean(), Is.True);
                      Assert.That(body.GetProperty("usedByIngredientNames").GetArrayLength(), Is.Zero);
                      Assert.That(listed.GetProperty("name").GetString(), Is.EqualTo("Portion"));
                    });
  }

  [Test]
  public async Task PostUnit_NoName_IsRefusedWithNameMissing()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/units", new { name = " " });

    await AssertValidationRefusalAsync(response, "errors.admin.units.nameMissing");
  }

  [Test]
  public async Task PostUnit_ANameAnActiveUnitHoldsInOtherCase_IsRefusedWithNameTaken()
  {
    await CreateUnitAsync("Portion");

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/units", new { name = "PORTION" });

    await AssertValidationRefusalAsync(response, "errors.admin.units.nameTaken");
    Assert.That(JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("parameters").GetProperty("name").GetString(), Is.EqualTo("PORTION"));
  }

  [Test]
  public async Task PostUnit_TheNameOfADeactivatedUnit_IsRefusedNamingThatUnit()
  {
    var unitId = await CreateUnitAsync("Portion");
    await DeactivateUnitAsync(unitId);

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/units", new { name = "portion" });

    await AssertValidationRefusalAsync(response, "errors.admin.units.nameBelongsToDeactivatedUnit");
    Assert.That(JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("parameters").GetProperty("name").GetString(), Is.EqualTo("Portion"));
  }

  [Test]
  public async Task GetUnits_TwoUnitsOneSwitchedOff_ListsBothByName()
  {
    await CreateUnitAsync("Portion");
    var packungId = await CreateUnitAsync("Packung");
    await DeactivateUnitAsync(packungId);

    var units = (await GetUnitsAsync()).GetProperty("units").EnumerateArray().Where(unit => unit.GetProperty("name").GetString() is "Packung" or "Portion").ToList();

    Assert.Multiple(() =>
                    {
                      Assert.That(units.Select(unit => unit.GetProperty("name").GetString()),
                                  Is.EqualTo(new[]
                                             {
                                               "Packung",
                                               "Portion"
                                             }));
                      Assert.That(units[0].GetProperty("isActive").GetBoolean(), Is.False);
                      Assert.That(units[1].GetProperty("isActive").GetBoolean(), Is.True);
                    });
  }

  [Test]
  public async Task GetUnits_AUnitAnIngredientUses_CarriesTheUsedIngredientNamesSortedByName()
  {
    var unitId = await CreateUnitAsync("Portion");
    await CreateIngredientAsync("Zwiebeln", unitId);
    await CreateIngredientAsync("Senf", unitId);

    var listed = await FindListedUnitAsync(unitId);
    List<string?> usedByIngredientNames = listed.GetProperty("usedByIngredientNames").EnumerateArray().Select(name => name.GetString()).ToList();

    Assert.That(usedByIngredientNames,
                Is.EqualTo(new[]
                           {
                             "Senf",
                             "Zwiebeln"
                           }));
  }

  [Test]
  public async Task PutUnit_ANewName_RenamesIt()
  {
    var unitId = await CreateUnitAsync("Portion");

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/units/{unitId}", new { name = "Packung" });

    var listed = await FindListedUnitAsync(unitId);
    var savedId = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("unitId").GetGuid();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(savedId, Is.EqualTo(unitId));
                      Assert.That(listed.GetProperty("name").GetString(), Is.EqualTo("Packung"));
                    });
  }

  [Test]
  public async Task PutUnit_AnUnknownUnit_AnswersNotFound()
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/units/{Guid.NewGuid()}", new { name = "Portion" });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }

  [Test]
  public async Task PutUnit_TheNameOfADeactivatedUnit_IsRefusedNamingThatUnit()
  {
    var deactivatedId = await CreateUnitAsync("Packung");
    await DeactivateUnitAsync(deactivatedId);
    var unitId = await CreateUnitAsync("Portion");

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/units/{unitId}", new { name = "packung" });

    await AssertValidationRefusalAsync(response, "errors.admin.units.nameBelongsToDeactivatedUnit");
    Assert.That(JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("parameters").GetProperty("name").GetString(), Is.EqualTo("Packung"));
  }

  [Test]
  public async Task PostDeactivateThenActivate_AnUnusedUnit_SwitchesItOffAndOnAgain()
  {
    var unitId = await CreateUnitAsync("Portion");

    using var deactivated = await _context.Client.PostAsync($"/api/admin/units/{unitId}/deactivate", null);
    var afterDeactivate = (await FindListedUnitAsync(unitId)).GetProperty("isActive").GetBoolean();
    using var activated = await _context.Client.PostAsync($"/api/admin/units/{unitId}/activate", null);
    var afterActivate = (await FindListedUnitAsync(unitId)).GetProperty("isActive").GetBoolean();

    Assert.Multiple(() =>
                    {
                      Assert.That(deactivated.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(activated.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(afterDeactivate, Is.False);
                      Assert.That(afterActivate, Is.True);
                    });
  }

  [Test]
  public async Task PostDeactivate_AUnitAnIngredientUses_IsRefusedAndStaysActive()
  {
    var unitId = await CreateUnitAsync("Portion");
    await CreateIngredientAsync("Senf", unitId);

    using var response = await _context.Client.PostAsync($"/api/admin/units/{unitId}/deactivate", null);

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    await using var database = _context.Factory.CreateContext();
    var stored = await database.Units.SingleAsync(unit => unit.Id == unitId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("UnitUsedByIngredients"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(stored.IsActive, Is.True);
                    });
  }

  [Test]
  public async Task PostDeactivate_AUnitOnlyADeactivatedIngredientUses_IsRefusedAndStillListsThatIngredient()
  {
    var unitId = await CreateUnitAsync("Portion");
    var ingredientId = await CreateIngredientAsync("Dill", unitId);

    using var deactivatedIngredient = await _context.Client.PostAsync($"/api/admin/ingredients/{ingredientId}/deactivate", null);
    using var response = await _context.Client.PostAsync($"/api/admin/units/{unitId}/deactivate", null);

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    var usedByIngredientNames = (await FindListedUnitAsync(unitId)).GetProperty("usedByIngredientNames").EnumerateArray().Select(name => name.GetString()).ToList();
    await using var database = _context.Factory.CreateContext();
    var stored = await database.Units.SingleAsync(unit => unit.Id == unitId);

    Assert.Multiple(() =>
                    {
                      Assert.That(deactivatedIngredient.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("UnitUsedByIngredients"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(usedByIngredientNames, Is.EqualTo(new[] { "Dill" }));
                      Assert.That(stored.IsActive, Is.True);
                    });
  }

  [Test]
  public async Task PostActivate_AnUnknownUnit_AnswersNotFound()
  {
    using var response = await _context.Client.PostAsync($"/api/admin/units/{Guid.NewGuid()}/activate", null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }

  private async Task<JsonElement> GetUnitsAsync()
  {
    return JsonDocument.Parse(await _context.Client.GetStringAsync("/api/admin/units")).RootElement.Clone();
  }

  private async Task<JsonElement> FindListedUnitAsync(Guid unitId)
  {
    var units = (await GetUnitsAsync()).GetProperty("units");

    return units.EnumerateArray().Single(unit => unit.GetProperty("unitId").GetGuid() == unitId);
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

  private async Task<Guid> CreateIngredientAsync(string name, Guid unitId)
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/ingredients",
                                                               new
                                                               {
                                                                 name,
                                                                 unitId
                                                               });

    var answer = await response.Content.ReadAsStringAsync();
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), answer);

    return JsonDocument.Parse(answer).RootElement.GetProperty("ingredientId").GetGuid();
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
