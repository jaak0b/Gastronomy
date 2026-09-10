using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminFestivalEndpointsTest
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

  private object PeriodOf(string name, int yearsFromNow, int lengthInHours = 24)
  {
    var startsAtUtc = DateTime.UtcNow.AddYears(yearsFromNow);

    return new
           {
             name,
             startsAtUtc = startsAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
             endsAtUtc = startsAtUtc.AddHours(lengthInHours).ToString("yyyy-MM-ddTHH:mm:ssZ")
           };
  }

  private async Task<Guid> CreateFestivalAsync(string name, int yearsFromNow, int lengthInHours = 24)
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/festivals",
                                                               PeriodOf(name, yearsFromNow, lengthInHours));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("festivalId").GetGuid();
  }

  private async Task<Guid> AddStationToTheFestivalAsync(string name, int sortOrder)
  {
    var stationId = Guid.NewGuid();

    await using (var database = _context.Factory.CreateContext())
    {
      database.Stations.Add(new()
                            {
                              Id = stationId,
                              Name = name,
                              SortOrder = sortOrder,
                              IsActive = true
                            });
      await database.SaveChangesAsync();
    }

    using var response =
      await _context.Client.PutAsync($"/api/admin/festivals/{_context.World.FestivalId}/stations/{stationId}", null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    return stationId;
  }

  [Test]
  public async Task GetFestivals_TheRawAnswer_CarriesBothMomentsWithATrailingZ()
  {
    using var response = await _context.Client.GetAsync("/api/admin/festivals");
    var raw = await response.Content.ReadAsStringAsync();
    var festival = JsonDocument.Parse(raw).RootElement.GetProperty("festivals")[0];

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(festival.GetProperty("startsAtUtc").GetString(), Does.EndWith("Z"));
                      Assert.That(festival.GetProperty("endsAtUtc").GetString(), Does.EndWith("Z"));
                    });
  }

  [Test]
  public async Task GetFestivals_TheSeededFestival_ReportsItAsRunningWithItsCounts()
  {
    using var response = await _context.Client.GetAsync("/api/admin/festivals");
    var festival = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
                               .RootElement.GetProperty("festivals")[0];

    Assert.Multiple(() =>
                    {
                      Assert.That(festival.GetProperty("isRunning").GetBoolean(), Is.True);
                      Assert.That(festival.GetProperty("isHidden").GetBoolean(), Is.False);
                      Assert.That(festival.GetProperty("stationCount").GetInt32(), Is.EqualTo(2));
                      Assert.That(festival.GetProperty("menuItemCount").GetInt32(), Is.EqualTo(2));
                      Assert.That(festival.GetProperty("orderCount").GetInt32(), Is.EqualTo(0));
                    });
  }

  [Test]
  public async Task GetFestivals_AHiddenFestival_IsStillListedSoTheShowButtonCanBeReached()
  {
    var festivalId = await CreateFestivalAsync("Herbstfest", 2);

    using (var hidden = await _context.Client.PostAsync($"/api/admin/festivals/{festivalId}/hide", null))
    {
      Assert.That(hidden.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await _context.Client.GetAsync("/api/admin/festivals");
    var festivals = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
                                .RootElement.GetProperty("festivals");

    var hiddenRow = festivals.EnumerateArray()
                             .Single(row => row.GetProperty("festivalId").GetGuid() == festivalId);

    Assert.That(hiddenRow.GetProperty("isHidden").GetBoolean(), Is.True);
  }

  [Test]
  public async Task PostFestival_NameMissing_IsRefusedWithTheNameKey()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/festivals", PeriodOf("   ", 2));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.festivalNameMissing"));
                    });
  }

  [Test]
  public async Task PostFestival_EndBeforeTheStart_IsRefusedWithThePeriodKey()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/festivals", PeriodOf("Herbstfest", 2, -3));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.festivalPeriodInvalid"));
                    });
  }

  [Test]
  public async Task PostFestival_APeriodAnotherFestivalAlreadyCovers_IsRefusedAndNamesThatFestival()
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/festivals", PeriodOf("Herbstfest", 0, 1));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("FestivalOverlaps"));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.festivalOverlaps"));
                      Assert.That(body.RootElement.GetProperty("parameters").GetProperty("name").GetString(),
                                  Is.EqualTo("Sommerfest"));
                    });
  }

  [Test]
  public async Task PutFestival_AFestivalThatIsNotThere_IsNotFound()
  {
    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/festivals/{Guid.NewGuid()}",
                                                              PeriodOf("Herbstfest", 2));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }

  [Test]
  public async Task PutFestival_ANewPeriodForTheFestivalItself_IsSaved()
  {
    var festivalId = await CreateFestivalAsync("Herbstfest", 2);

    using var response = await _context.Client.PutAsJsonAsync($"/api/admin/festivals/{festivalId}",
                                                              PeriodOf("Herbstfest am See", 3));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    await using var database = _context.Factory.CreateContext();
    var festival = await database.Festivals.FirstAsync(candidate => candidate.Id == festivalId);

    Assert.That(festival.Name, Is.EqualTo("Herbstfest am See"));
  }

  [Test]
  public async Task PostHide_AFestivalThatIsRunning_IsRefusedGenerically()
  {
    using var response = await _context.Client.PostAsync($"/api/admin/festivals/{_context.World.FestivalId}/hide", null);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.actionFailed"));
                    });
  }

  [Test]
  public async Task PostFestival_APeriodAHiddenFestivalCovers_IsRefusedAndNamesThatFestival()
  {
    var hiddenFestivalId = await CreateFestivalAsync("Herbstfest", 2);

    using (var hidden = await _context.Client.PostAsync($"/api/admin/festivals/{hiddenFestivalId}/hide", null))
    {
      Assert.That(hidden.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await _context.Client.PostAsJsonAsync("/api/admin/festivals", PeriodOf("Weinfest", 2));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.festivalOverlaps"));
                      Assert.That(body.RootElement.GetProperty("parameters").GetProperty("name").GetString(),
                                  Is.EqualTo("Herbstfest"));
                    });
  }

  [Test]
  public async Task PostShow_AFestivalAnotherOneNowCovers_PutsItBackOnTheList()
  {
    var hiddenFestivalId = await CreateFestivalAsync("Herbstfest", 2);

    using (var hidden = await _context.Client.PostAsync($"/api/admin/festivals/{hiddenFestivalId}/hide", null))
    {
      Assert.That(hidden.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    await using (var database = _context.Factory.CreateContext())
    {
      var hiddenFestival = await database.Festivals.FirstAsync(candidate => candidate.Id == hiddenFestivalId);

      database.Festivals.Add(new()
                             {
                               Id = Guid.NewGuid(),
                               Name = "Weinfest",
                               StartsAtUtc = hiddenFestival.StartsAtUtc,
                               EndsAtUtc = hiddenFestival.EndsAtUtc,
                               NextOrderNumber = 1,
                               IsHidden = false
                             });
      await database.SaveChangesAsync();
    }

    using var response = await _context.Client.PostAsync($"/api/admin/festivals/{hiddenFestivalId}/show", null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    await using var reread = _context.Factory.CreateContext();
    var festival = await reread.Festivals.FirstAsync(candidate => candidate.Id == hiddenFestivalId);

    Assert.That(festival.IsHidden, Is.False);
  }

  [Test]
  public async Task PostShow_AHiddenFestivalNothingIsInTheWayOf_PutsItBackOnTheList()
  {
    var festivalId = await CreateFestivalAsync("Herbstfest", 2);

    using (var hidden = await _context.Client.PostAsync($"/api/admin/festivals/{festivalId}/hide", null))
    {
      Assert.That(hidden.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await _context.Client.PostAsync($"/api/admin/festivals/{festivalId}/show", null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    await using var database = _context.Factory.CreateContext();
    var festival = await database.Festivals.FirstAsync(candidate => candidate.Id == festivalId);

    Assert.That(festival.IsHidden, Is.False);
  }

  [Test]
  public async Task PostCopy_AFestivalWithStationsAndAMenu_BringsThemAlongAndStartsCountingAtOne()
  {
    using var response = await _context.Client.PostAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/copy",
                                                               PeriodOf("Sommerfest im naechsten Jahr", 2));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var copyId = body.RootElement.GetProperty("festivalId").GetGuid();

    await using var database = _context.Factory.CreateContext();
    var copy = await database.Festivals.FirstAsync(candidate => candidate.Id == copyId);
    List<FestivalStation> stationLinks = await database.FestivalStations
                                                       .Where(link => link.FestivalId == copyId)
                                                       .ToListAsync();
    List<FestivalCatalogItem> menuRows = await database.FestivalCatalogItems
                                                       .Where(menuRow => menuRow.FestivalId == copyId)
                                                       .ToListAsync();
    var assignments = await database.ItemStationAssignments.CountAsync(assignment => assignment.FestivalId == copyId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(copyId, Is.Not.EqualTo(_context.World.FestivalId));
                      Assert.That(copy.NextOrderNumber, Is.EqualTo(1));
                      Assert.That(copy.IsHidden, Is.False);
                      Assert.That(stationLinks, Has.Count.EqualTo(2));
                      Assert.That(stationLinks.Select(link => link.NextStationOrderNumber), Is.All.EqualTo(1));
                      Assert.That(menuRows, Has.Count.EqualTo(2));
                      Assert.That(menuRows.Select(menuRow => menuRow.IsAvailable), Is.All.True);
                      Assert.That(menuRows.Select(menuRow => menuRow.PriceCents), Is.EquivalentTo(new[] { 350, 300 }));
                      Assert.That(assignments, Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task PutFestivalItem_AnItemThatIsNotOnTheMenuYet_PutsItOnAsAvailable()
  {
    var pommesId = await CreateItemAsync("Pommes");

    using var response =
      await _context.Client.PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/items/{pommesId}",
                                           new
                                           {
                                             priceCents = 250,
                                             stationIds = new[] { _context.World.KitchenStationId }
                                           });

    await using var database = _context.Factory.CreateContext();
    var menuRow = await database.FestivalCatalogItems
                                .FirstAsync(candidate => candidate.FestivalId == _context.World.FestivalId
                                                         && candidate.CatalogItemId == pommesId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(menuRow.PriceCents, Is.EqualTo(250));
                      Assert.That(menuRow.IsAvailable, Is.True);
                    });
  }

  [Test]
  public async Task PutFestivalItem_AnItemThatIsSoldOut_KeepsItSoldOutWhileTheNewPriceIsSaved()
  {
    using (var soldOut = await _context.Client
                                       .PostAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/items/{_context.World.BratwurstItemId}/availability",
                                                        new { isAvailable = false }))
    {
      Assert.That(soldOut.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response =
      await _context.Client.PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/items/{_context.World.BratwurstItemId}",
                                           new
                                           {
                                             priceCents = 400,
                                             stationIds = new[] { _context.World.KitchenStationId }
                                           });

    await using var database = _context.Factory.CreateContext();
    var menuRow = await database.FestivalCatalogItems
                                .FirstAsync(candidate => candidate.FestivalId == _context.World.FestivalId
                                                         && candidate.CatalogItemId == _context.World.BratwurstItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(menuRow.PriceCents, Is.EqualTo(400));
                      Assert.That(menuRow.IsAvailable, Is.False);
                    });
  }

  [Test]
  public async Task PutFestivalItem_ANewListOfStations_ReplacesTheOldOne()
  {
    using var response =
      await _context.Client.PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/items/{_context.World.BratwurstItemId}",
                                           new
                                           {
                                             priceCents = 350,
                                             stationIds = new[] { _context.World.BarStationId }
                                           });

    await using var database = _context.Factory.CreateContext();
    List<Guid> stationIds = await database.ItemStationAssignments
                                          .Where(assignment => assignment.FestivalId == _context.World.FestivalId
                                                               && assignment.CatalogItemId == _context.World.BratwurstItemId)
                                          .Select(assignment => assignment.StationId)
                                          .ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stationIds, Is.EquivalentTo(new[] { _context.World.BarStationId }));
                    });
  }

  [Test]
  public async Task PutFestivalItem_NoStationAtAll_IsRefusedWithTheStationKey()
  {
    using var response =
      await _context.Client.PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/items/{_context.World.BratwurstItemId}",
                                           new { priceCents = 350, stationIds = Array.Empty<Guid>() });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.itemNeedsAStation"));
                    });
  }

  [Test]
  public async Task PutFestivalItem_APriceAboveTheHighestOne_IsRefusedWithThePriceKey()
  {
    using var response =
      await _context.Client.PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/items/{_context.World.BratwurstItemId}",
                                           new
                                           {
                                             priceCents = 100000,
                                             stationIds = new[] { _context.World.KitchenStationId }
                                           });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.itemPriceOutOfRange"));
                    });
  }

  [Test]
  public async Task PutFestivalItem_AStationThatIsNotAtThatFestival_IsRefusedGenerically()
  {
    var strangerId = Guid.NewGuid();

    await using (var database = _context.Factory.CreateContext())
    {
      database.Stations.Add(new()
                            {
                              Id = strangerId,
                              Name = "Weinstand",
                              SortOrder = 9,
                              IsActive = true
                            });
      await database.SaveChangesAsync();
    }

    using var response =
      await _context.Client.PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/items/{_context.World.BratwurstItemId}",
                                           new { priceCents = 350, stationIds = new[] { strangerId } });

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.actionFailed"));
                    });
  }

  [Test]
  public async Task DeleteFestivalItem_AnItemOnTheMenu_TakesItsAssignmentsWithIt()
  {
    using var response =
      await _context.Client.DeleteAsync($"/api/admin/festivals/{_context.World.FestivalId}/items/{_context.World.BratwurstItemId}");

    await using var database = _context.Factory.CreateContext();
    var menuRows = await database.FestivalCatalogItems
                                 .CountAsync(menuRow => menuRow.FestivalId == _context.World.FestivalId
                                                        && menuRow.CatalogItemId == _context.World.BratwurstItemId);
    var assignments = await database.ItemStationAssignments
                                    .CountAsync(assignment => assignment.FestivalId == _context.World.FestivalId
                                                              && assignment.CatalogItemId == _context.World.BratwurstItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
                      Assert.That(menuRows, Is.EqualTo(0));
                      Assert.That(assignments, Is.EqualTo(0));
                    });
  }

  [Test]
  public async Task PutFestivalStation_AStationThatIsNotThereYet_LinksItAndStartsItsCounterAtOne()
  {
    var stationId = await AddStationToTheFestivalAsync("Kuchenbuffet", 3);

    await using var database = _context.Factory.CreateContext();
    var link = await database.FestivalStations
                             .FirstAsync(candidate => candidate.FestivalId == _context.World.FestivalId
                                                      && candidate.StationId == stationId);

    Assert.That(link.NextStationOrderNumber, Is.EqualTo(1));
  }

  [Test]
  public async Task DeleteFestivalStation_AStationThatNeverServed_TakesItOffTheFestival()
  {
    var stationId = await AddStationToTheFestivalAsync("Kuchenbuffet", 3);

    using var response =
      await _context.Client.DeleteAsync($"/api/admin/festivals/{_context.World.FestivalId}/stations/{stationId}");

    await using var database = _context.Factory.CreateContext();
    var links = await database.FestivalStations
                              .CountAsync(link => link.FestivalId == _context.World.FestivalId
                                                  && link.StationId == stationId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
                      Assert.That(links, Is.EqualTo(0));
                    });
  }

  [Test]
  public async Task DeleteFestivalStation_AStationWhoseSliceIsAlreadyFinished_IsStillRefused()
  {
    using (var placed = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    await using (var database = _context.Factory.CreateContext())
    {
      await database.OrderItems.ExecuteUpdateAsync(item => item.SetProperty(entry => entry.ProductionStatus,
                                                                            ProductionStatus.Finished));
    }

    using var response =
      await _context.Client.DeleteAsync($"/api/admin/festivals/{_context.World.FestivalId}/stations/{_context.World.KitchenStationId}");

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("code").GetString(),
                                  Is.EqualTo("StationHasOrdersAtTheFestival"));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.stationHasOrdersAtTheFestival"));
                      Assert.That(body.RootElement.GetProperty("parameters").GetProperty("count").GetString(),
                                  Is.EqualTo("1"));
                    });
  }

  [Test]
  public async Task DeleteFestivalStation_TheOnlyStationAnItemOnTheMenuHas_IsRefused()
  {
    var stationId = await AddStationToTheFestivalAsync("Kuchenbuffet", 3);
    var cakeId = await CreateItemAsync("Kuchen");

    using (var putOn = await _context.Client
                                     .PutAsJsonAsync($"/api/admin/festivals/{_context.World.FestivalId}/items/{cakeId}",
                                                     new { priceCents = 200, stationIds = new[] { stationId } }))
    {
      Assert.That(putOn.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response =
      await _context.Client.DeleteAsync($"/api/admin/festivals/{_context.World.FestivalId}/stations/{stationId}");

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("admin.itemsWouldHaveNoStation"));
                    });
  }

  [Test]
  public async Task GetItems_WithAFestival_ReportsThePriceAndTheStationsOfThatFestival()
  {
    using var response = await _context.Client.GetAsync($"/api/admin/items?festivalId={_context.World.FestivalId}");
    var items = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("items");

    var bratwurst = items.EnumerateArray()
                         .Single(item => item.GetProperty("itemId").GetGuid() == _context.World.BratwurstItemId);
    var atTheFestival = bratwurst.GetProperty("atTheFestival");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(atTheFestival.GetProperty("priceCents").GetInt32(), Is.EqualTo(350));
                      Assert.That(atTheFestival.GetProperty("isAvailable").GetBoolean(), Is.True);
                      Assert.That(atTheFestival.GetProperty("stationIds").GetArrayLength(), Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task GetItems_WithoutAFestival_LeavesEveryRowWithoutAFestivalSide()
  {
    using var response = await _context.Client.GetAsync("/api/admin/items");
    var items = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("items");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(items.EnumerateArray()
                                       .Select(item => item.GetProperty("atTheFestival").ValueKind),
                                  Is.All.EqualTo(JsonValueKind.Null));
                    });
  }

  [Test]
  public async Task GetItems_AFestivalThatIsNotThere_IsNotFound()
  {
    using var response = await _context.Client.GetAsync($"/api/admin/items?festivalId={Guid.NewGuid()}");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }

  [Test]
  public async Task GetStations_WithAFestival_MarksTheOnesThatBelongToIt()
  {
    var strangerId = Guid.NewGuid();

    await using (var database = _context.Factory.CreateContext())
    {
      database.Stations.Add(new()
                            {
                              Id = strangerId,
                              Name = "Weinstand",
                              SortOrder = 9,
                              IsActive = true
                            });
      await database.SaveChangesAsync();
    }

    using var response = await _context.Client.GetAsync($"/api/admin/stations?festivalId={_context.World.FestivalId}");
    var stations = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("stations");

    var kitchen = stations.EnumerateArray()
                          .Single(station => station.GetProperty("stationId").GetGuid() == _context.World.KitchenStationId);
    var stranger = stations.EnumerateArray()
                           .Single(station => station.GetProperty("stationId").GetGuid() == strangerId);

    Assert.Multiple(() =>
                    {
                      Assert.That(kitchen.GetProperty("isAtTheFestival").GetBoolean(), Is.True);
                      Assert.That(stranger.GetProperty("isAtTheFestival").GetBoolean(), Is.False);
                    });
  }

  [Test]
  public async Task GetStations_WithoutAFestival_LeavesEveryRowOutsideOne()
  {
    using var response = await _context.Client.GetAsync("/api/admin/stations");
    var stations = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("stations");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stations.EnumerateArray()
                                          .Select(station => station.GetProperty("isAtTheFestival").GetBoolean()),
                                  Is.All.False);
                    });
  }

  private async Task<Guid> CreateItemAsync(string name)
  {
    using var response = await _context.Client.PostAsJsonAsync("/api/admin/items",
                                                               new
                                                               {
                                                                 name,
                                                                 categoryId = _context.World.FoodCategoryId,
                                                                 sortOrder = 9
                                                               });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("itemId").GetGuid();
  }
}
