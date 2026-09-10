using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.Endpoints;
using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class OrderableItemsTest
{

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
  public async Task IdsAt_ArticleWhoseOnlyStationIsSwitchedOff_LeavesThatArticleOut()
  {
    await StationSwitchedOffAtTheFestivalAsync();

    await using (var writeContext = _context.Factory.CreateContext())
    {
      var kitchen = await writeContext.Stations.FirstAsync(station => station.Id == _context.World.KitchenStationId);
      kitchen.IsActive = false;
      await writeContext.SaveChangesAsync();
    }

    await using var readContext = _context.Factory.CreateContext();
    OrderableItems orderableItems = new(_context.Factory.Services.GetRequiredService<IClock>());

    IReadOnlyList<Guid> itemIds = await orderableItems.IdsAtAsync(readContext,
                                                                  _context.World.FestivalId,
                                                                  CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(itemIds, Does.Not.Contain(_context.World.BratwurstItemId));
                      Assert.That(itemIds, Does.Contain(_context.World.BeerItemId));
                    });
  }

  [Test]
  public async Task GetCatalog_ArticleSwitchedBackOnAfterItsOnlyStationWasSwitchedOff_LeavesThatArticleOut()
  {
    await PostAsync($"/api/admin/items/{_context.World.BratwurstItemId}/deactivate");
    await PostAsync($"/api/admin/stations/{_context.World.KitchenStationId}/deactivate");
    await PostAsync($"/api/admin/items/{_context.World.BratwurstItemId}/activate");

    Assert.That(await ItemIdsInTheCatalogAsync(), Does.Not.Contain(_context.World.BratwurstItemId));
  }

  [Test]
  public async Task GetCatalog_CopiedFestivalCarryingAnAssignmentToASwitchedOffStation_LeavesThatArticleOut()
  {
    await using (var writeContext = _context.Factory.CreateContext())
    {
      var lastYear = await writeContext.Festivals.FirstAsync(candidate => candidate.Id == _context.World.FestivalId);
      lastYear.StartsAtUtc = DateTime.UtcNow.AddDays(-10);
      lastYear.EndsAtUtc = DateTime.UtcNow.AddDays(-9);
      await writeContext.SaveChangesAsync();
    }

    await PostAsync($"/api/admin/stations/{_context.World.KitchenStationId}/deactivate");

    using (var copied = await _context.Client
                                      .PostAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/copy",
                                                       new
                                                       {
                                                         name = "Sommerfest dieses Jahr",
                                                         startsAtUtc = DateTime.UtcNow.AddHours(-1),
                                                         endsAtUtc = DateTime.UtcNow.AddDays(1)
                                                       }))
    {
      Assert.That(copied.StatusCode,
                  Is.EqualTo(HttpStatusCode.Created),
                  await copied.Content.ReadAsStringAsync());
    }

    IReadOnlyList<Guid> itemIds = await ItemIdsInTheCatalogAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(itemIds, Does.Not.Contain(_context.World.BratwurstItemId));
                      Assert.That(itemIds, Does.Contain(_context.World.BeerItemId));
                    });
  }

  [Test]
  public async Task GetCatalog_ArticleWithOneActiveAndOneSwitchedOffStation_KeepsItAtTheActiveStation()
  {
    using (var assigned = await _context.Client
                                        .PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/items/{_context.World.BratwurstItemId}",
                                                        new
                                                        {
                                                          priceCents = 350,
                                                          stationIds = new[]
                                                                       {
                                                                         _context.World.KitchenStationId,
                                                                         _context.World.BarStationId
                                                                       }
                                                        }))
    {
      Assert.That(assigned.StatusCode, Is.EqualTo(HttpStatusCode.OK), await assigned.Content.ReadAsStringAsync());
    }

    await PostAsync($"/api/admin/stations/{_context.World.KitchenStationId}/deactivate");

    var body = await GetCatalogAsync();
    JsonElement bratwurst = body.RootElement.GetProperty("items")
                                .EnumerateArray()
                                .Single(item => item.GetProperty("id").GetGuid() == _context.World.BratwurstItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(bratwurst.GetProperty("stationIds").GetArrayLength(), Is.EqualTo(1));
                      Assert.That(bratwurst.GetProperty("stationIds")[0].GetGuid(),
                                  Is.EqualTo(_context.World.BarStationId));
                    });
  }

  [Test]
  public async Task PutOnTheMenu_WithOnlyAStationThatIsSwitchedOff_IsRefusedAndLeavesTheItemOrderable()
  {
    var switchedOffStationId = await StationSwitchedOffAtTheFestivalAsync();

    using (var response = await _context.Client
                                        .PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/items/{_context.World.BratwurstItemId}",
                                                        new
                                                        {
                                                          priceCents = 350,
                                                          stationIds = new[] { switchedOffStationId }
                                                        }))
    {
      var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

      Assert.Multiple(() =>
                      {
                        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                        Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                    Is.EqualTo("admin.itemNeedsAStation"));
                      });
    }

    using var placed = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));

    Assert.That(placed.StatusCode,
                Is.EqualTo(HttpStatusCode.Created),
                $"The refused menu change must leave the item at its station. Body: {await placed.Content.ReadAsStringAsync()}");
  }

  [Test]
  public async Task RemoveStationFromFestival_WhenTheOtherStationIsSwitchedOff_IsRefusedAndLeavesTheItemOrderable()
  {
    await StationSwitchedOffAtTheFestivalAsync(_context.World.KitchenStationId);

    using (var response = await _context.Client
                                        .DeleteAsync($"/api/admin/festivals/{_context.World.FestivalId}/stations/{_context.World.KitchenStationId}"))
    {
      var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

      Assert.Multiple(() =>
                      {
                        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                        Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                    Is.EqualTo("admin.itemsWouldHaveNoStation"));
                        Assert.That(body.RootElement.GetProperty("parameters").GetProperty("count").GetString(),
                                    Is.EqualTo("1"));
                      });
    }

    using var placed = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));

    Assert.That(placed.StatusCode,
                Is.EqualTo(HttpStatusCode.Created),
                $"The refused removal must leave the item at its station. Body: {await placed.Content.ReadAsStringAsync()}");
  }

  private async Task<Guid> StationSwitchedOffAtTheFestivalAsync(Guid? alsoPreparingWith = null)
  {
    Guid stationId;

    using (var created = await _context.Client.PostAsJsonAsync("/api/admin/stations",
                                                               new { name = "Zelt", sortOrder = 3 }))
    {
      Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
      stationId = JsonDocument.Parse(await created.Content.ReadAsStringAsync())
                              .RootElement.GetProperty("stationId")
                              .GetGuid();
    }

    using (var added = await _context.Client
                                     .PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/stations/{stationId}",
                                                     new { }))
    {
      Assert.That(added.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    if (alsoPreparingWith is { } partnerStationId)
    {
      using var assigned = await _context.Client
                                         .PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/items/{_context.World.BratwurstItemId}",
                                                         new
                                                         {
                                                           priceCents = 350,
                                                           stationIds = new[] { partnerStationId, stationId }
                                                         });

      Assert.That(assigned.StatusCode,
                  Is.EqualTo(HttpStatusCode.OK),
                  await assigned.Content.ReadAsStringAsync());
    }

    using (var switchedOff = await _context.Client.PostAsync($"/api/admin/stations/{stationId}/deactivate", null))
    {
      Assert.That(switchedOff.StatusCode,
                  Is.EqualTo(HttpStatusCode.OK),
                  await switchedOff.Content.ReadAsStringAsync());
    }

    return stationId;
  }

  private async Task PostAsync(string route)
  {
    using var response = await _context.Client.PostAsync(route, null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
  }

  private async Task<IReadOnlyList<Guid>> ItemIdsInTheCatalogAsync()
  {
    var body = await GetCatalogAsync();

    return
    [
      .. body.RootElement.GetProperty("items")
             .EnumerateArray()
             .Select(item => item.GetProperty("id").GetGuid())
    ];
  }

  private async Task<JsonDocument> GetCatalogAsync()
  {
    using var response = await _context.SendAsync(HttpMethod.Get, "/api/catalog");
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
  }
}
