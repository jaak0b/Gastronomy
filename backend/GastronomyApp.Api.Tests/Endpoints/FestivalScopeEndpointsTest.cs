using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class FestivalScopeEndpointsTest
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

  private async Task LetTheFestivalEndAsync()
  {
    await using var database = _context.Factory.CreateContext();
    await database.Festivals
                  .Where(festival => festival.Id == _context.World.FestivalId)
                  .ExecuteUpdateAsync(festival => festival.SetProperty(entry => entry.EndsAtUtc,
                                                                        DateTime.UtcNow.AddMinutes(-1)));
  }

  private async Task TakeTheKitchenOffTheFestivalAsync()
  {
    await using var database = _context.Factory.CreateContext();
    await database.FestivalStations
                  .Where(link => link.FestivalId == _context.World.FestivalId
                                 && link.StationId == _context.World.KitchenStationId)
                  .ExecuteDeleteAsync();
  }

  private async Task<JsonDocument> BodyOfAsync(HttpResponseMessage response)
  {
    return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
  }

  [Test]
  public async Task GetCatalog_AFestivalIsRunning_NamesItSoThePhoneCanTellFestivalsApart()
  {
    using var response = await _context.SendAsync(HttpMethod.Get, "/api/catalog");
    var body = await BodyOfAsync(response);
    var festival = body.RootElement.GetProperty("festival");

    Assert.Multiple(() =>
                    {
                      Assert.That(festival.GetProperty("festivalId").GetGuid(), Is.EqualTo(_context.World.FestivalId));
                      Assert.That(festival.GetProperty("name").GetString(), Is.EqualTo("Sommerfest"));
                    });
  }

  [Test]
  public async Task GetCatalog_NoFestivalIsRunning_AnswersWithNoFestivalAndAnEmptyMenu()
  {
    await LetTheFestivalEndAsync();

    using var response = await _context.SendAsync(HttpMethod.Get, "/api/catalog");
    var body = await BodyOfAsync(response);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("festival").ValueKind, Is.EqualTo(JsonValueKind.Null));
                      Assert.That(body.RootElement.GetProperty("categories").GetArrayLength(), Is.EqualTo(0));
                      Assert.That(body.RootElement.GetProperty("items").GetArrayLength(), Is.EqualTo(0));
                      Assert.That(body.RootElement.GetProperty("stations").GetArrayLength(), Is.EqualTo(0));
                    });
  }

  [Test]
  public async Task GetStations_NoFestivalIsRunning_AnswersWithAnEmptyList()
  {
    await LetTheFestivalEndAsync();

    using var response = await _context.SendAsync(HttpMethod.Get, "/api/stations");
    var body = await BodyOfAsync(response);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("stations").GetArrayLength(), Is.EqualTo(0));
                    });
  }

  [Test]
  public async Task GetEstimates_NoFestivalIsRunning_AnswersWithAnEmptyList()
  {
    await LetTheFestivalEndAsync();

    using var response = await _context.SendAsync(HttpMethod.Get, "/api/estimates");
    var body = await BodyOfAsync(response);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("stations").GetArrayLength(), Is.EqualTo(0));
                    });
  }

  [Test]
  public async Task GetOpenItems_NoFestivalIsRunning_AnswersWithNoTables()
  {
    using (var placed = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    await LetTheFestivalEndAsync();

    using var response = await _context.SendAsync(HttpMethod.Get, "/api/open-items");
    var body = await BodyOfAsync(response);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("tables").GetArrayLength(), Is.EqualTo(0));
                    });
  }

  [Test]
  public async Task GetTableNames_NoFestivalIsRunning_AnswersWithNoNames()
  {
    using (var placed = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    await LetTheFestivalEndAsync();

    using var response = await _context.SendAsync(HttpMethod.Get, "/api/open-items/table-names");
    var body = await BodyOfAsync(response);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("tableNames").GetArrayLength(), Is.EqualTo(0));
                    });
  }

  [Test]
  public async Task PostOrder_NoFestivalIsRunning_IsRefusedAsSomethingThePhoneCannotFix()
  {
    await LetTheFestivalEndAsync();

    using var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));
    var body = await BodyOfAsync(response);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("UnprocessableEntity"));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("order.cannotBeProcessed"));
                    });
  }

  [Test]
  public async Task PostOrder_ARetryOfAnOrderTheLaptopHoldsAfterTheFestivalEnded_StillAnswersWithThatOrder()
  {
    var body = _context.BuildOrder(Guid.NewGuid());

    string firstBody;
    using (var first = await _context.PostOrderAsync(body))
    {
      firstBody = await first.Content.ReadAsStringAsync();
      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    await LetTheFestivalEndAsync();

    using var retry = await _context.PostOrderAsync(body);
    var retryBody = await retry.Content.ReadAsStringAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(retry.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(retryBody, Is.EqualTo(firstBody));
                    });
  }

  [Test]
  public async Task PostSettle_NoFestivalIsRunning_IsRefusedAsSomethingThePhoneCannotFix()
  {
    using (var placed = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    List<Guid> orderItemIds;

    await using (var database = _context.Factory.CreateContext())
    {
      orderItemIds = await database.OrderItems.Select(item => item.Id).ToListAsync();
    }

    await LetTheFestivalEndAsync();

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody(orderItemIds, 700));

    var body = await BodyOfAsync(response);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("ValidationFailed"));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("order.settlementCannotBeProcessed"));
                    });
  }

  [Test]
  public async Task GetStationOrders_NoFestivalIsRunning_IsRefusedSoTheTabletCanSaySo()
  {
    var stationToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);
    await LetTheFestivalEndAsync();

    using var response = await _context.SendAsAsync(stationToken, HttpMethod.Get, "/api/station/orders");
    var body = await BodyOfAsync(response);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("NoRunningFestival"));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("station.noFestivalIsRunning"));
                    });
  }

  [Test]
  public async Task GetStationOrders_TheStationIsNotAtTheRunningFestival_IsRefusedSoTheTabletCanSaySo()
  {
    var stationToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);
    await TakeTheKitchenOffTheFestivalAsync();

    using var response = await _context.SendAsAsync(stationToken, HttpMethod.Get, "/api/station/orders");
    var body = await BodyOfAsync(response);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("code").GetString(),
                                  Is.EqualTo("StationNotAtTheFestival"));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("station.notPartOfTheFestival"));
                    });
  }

  [Test]
  public async Task PostStationItemStatus_NoFestivalIsRunning_ChangesNothingAndIsRefused()
  {
    var stationToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);

    using (var placed = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    List<Guid> orderItemIds;

    await using (var database = _context.Factory.CreateContext())
    {
      orderItemIds = await database.OrderItems.Select(item => item.Id).ToListAsync();
    }

    await LetTheFestivalEndAsync();

    using var response = await _context.SendAsAsync(stationToken,
                                                    HttpMethod.Post,
                                                    "/api/station/items/status",
                                                    new { orderItemIds, status = "inProduction" });

    var body = await BodyOfAsync(response);

    await using var afterwards = _context.Factory.CreateContext();
    var stillWaiting = await afterwards.OrderItems
                                       .CountAsync(item => item.ProductionStatus == Core.Enums.ProductionStatus.Waiting);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("station.noFestivalIsRunning"));
                      Assert.That(stillWaiting, Is.EqualTo(orderItemIds.Count));
                    });
  }
}
