using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class TablePlanEndpointsTest
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

  private string FestivalPath => $"/api/admin/festivals/{_context.World.FestivalId}";

  [Test]
  public async Task GetTablePlan_ARunningFestivalWithAPlan_AnswersTheShownAreasWithTheirTablesAndStations()
  {
    var gardenId = await CreateAreaAsync("Garten");
    var hallId = await CreateAreaAsync("Halle");
    await CreateTableAsync(hallId, "Stammtisch");
    var tableId = await CreateTableAsync(gardenId, "Tisch 1");
    var hiddenTableId = await CreateTableAsync(gardenId, "Tisch 2");
    await HideAsync($"{FestivalPath}/areas/{hallId}/hide");
    await HideAsync($"{FestivalPath}/tables/{hiddenTableId}/hide");
    await PlaceAsync(_context.World.KitchenStationId, gardenId);

    using var response = await _context.SendAsync(HttpMethod.Get, "/api/table-plan");
    var areas = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("areas");
    var garden = areas[0];
    var table = garden.GetProperty("tables")[0];
    var tableGeometry = table.GetProperty("geometry");
    var stationShape = garden.GetProperty("stationShapes")[0];

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(areas.GetArrayLength(), Is.EqualTo(1));
                      Assert.That(garden.GetProperty("areaId").GetGuid(), Is.EqualTo(gardenId));
                      Assert.That(garden.GetProperty("name").GetString(), Is.EqualTo("Garten"));
                      Assert.That(garden.GetProperty("tables").GetArrayLength(), Is.EqualTo(1));
                      Assert.That(table.GetProperty("tableId").GetGuid(), Is.EqualTo(tableId));
                      Assert.That(table.GetProperty("name").GetString(), Is.EqualTo("Tisch 1"));
                      Assert.That(tableGeometry.GetProperty("x").GetDouble(), Is.EqualTo(12.5));
                      Assert.That(tableGeometry.GetProperty("y").GetDouble(), Is.EqualTo(-7.25));
                      Assert.That(tableGeometry.GetProperty("width").GetDouble(), Is.EqualTo(80));
                      Assert.That(tableGeometry.GetProperty("height").GetDouble(), Is.EqualTo(40.5));
                      Assert.That(tableGeometry.GetProperty("rotation").GetDouble(), Is.EqualTo(-30));
                      Assert.That(garden.GetProperty("stationShapes").GetArrayLength(), Is.EqualTo(1));
                      Assert.That(stationShape.GetProperty("stationName").GetString(), Is.EqualTo("Kueche"));
                      Assert.That(stationShape.GetProperty("geometry").GetProperty("x").GetDouble(), Is.EqualTo(200.75));
                      Assert.That(stationShape.GetProperty("geometry").GetProperty("rotation").GetDouble(), Is.EqualTo(-135));
                    });
  }

  [Test]
  public async Task GetTablePlan_TablesAtDifferentPoints_CarryHowFarTheirOrdersAreHandedOutAndSettled()
  {
    var gardenId = await CreateAreaAsync("Garten");
    var partlyHandedOutId = await CreateTableAsync(gardenId, "Tisch 1");
    var untouchedId = await CreateTableAsync(gardenId, "Tisch 2");
    await CreateTableAsync(gardenId, "Tisch 3");
    var givenAwayId = await CreateTableAsync(gardenId, "Tisch 4");
    IReadOnlyList<Guid> partlyHandedOutItemIds = await PlaceOrderAtAsync(partlyHandedOutId);
    await PlaceOrderAtAsync(untouchedId);
    IReadOnlyList<Guid> givenAwayItemIds = await PlaceOrderAtAsync(givenAwayId);
    await HandOutAsync(partlyHandedOutItemIds[0]);
    await SettleAsync(partlyHandedOutItemIds);
    await GiveAwayAsync(givenAwayItemIds);

    using var response = await _context.SendAsync(HttpMethod.Get, "/api/table-plan");
    var tables = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("areas")[0].GetProperty("tables");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(tables.EnumerateArray().Select(table => table.GetProperty("name").GetString()),
                                  Is.EqualTo(new[]
                                             {
                                               "Tisch 1",
                                               "Tisch 2",
                                               "Tisch 3",
                                               "Tisch 4"
                                             }));
                      Assert.That(tables[0].GetProperty("handOutProgress").GetString(), Is.EqualTo("some"));
                      Assert.That(tables[0].GetProperty("settlementProgress").GetString(), Is.EqualTo("all"));
                      Assert.That(tables[1].GetProperty("handOutProgress").GetString(), Is.EqualTo("none"));
                      Assert.That(tables[1].GetProperty("settlementProgress").GetString(), Is.EqualTo("none"));
                      Assert.That(tables[2].GetProperty("handOutProgress").GetString(), Is.EqualTo("all"));
                      Assert.That(tables[2].GetProperty("settlementProgress").GetString(), Is.EqualTo("all"));
                      Assert.That(tables[3].GetProperty("handOutProgress").GetString(), Is.EqualTo("none"));
                      Assert.That(tables[3].GetProperty("settlementProgress").GetString(), Is.EqualTo("all"));
                    });
  }

  [Test]
  public async Task GetTablePlan_ARunningFestivalWhoseOnlyAreaIsHidden_AnswersNoArea()
  {
    using var response = await _context.SendAsync(HttpMethod.Get, "/api/table-plan");
    var areas = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("areas");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(areas.GetArrayLength(), Is.Zero);
                    });
  }

  [Test]
  public async Task GetTablePlan_NoFestivalIsRunning_AnswersNoArea()
  {
    var gardenId = await CreateAreaAsync("Garten");
    await CreateTableAsync(gardenId, "Tisch 1");
    await LetTheFestivalEndAsync();

    using var response = await _context.SendAsync(HttpMethod.Get, "/api/table-plan");
    var areas = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("areas");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(areas.GetArrayLength(), Is.Zero);
                    });
  }

  [Test]
  public async Task GetTablePlan_AStationTablet_IsRefusedAsTheWrongKindOfDevice()
  {
    var stationToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);

    using var response = await _context.SendAsAsync(stationToken, HttpMethod.Get, "/api/table-plan");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.auth.wrongDeviceKind"));
                    });
  }

  [Test]
  public async Task GetTablePlan_AnUnknownDevice_IsRefused()
  {
    using var response = await _context.SendAsAsync("unknownlookup.secret", HttpMethod.Get, "/api/table-plan");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  private async Task<Guid> CreateAreaAsync(string name)
  {
    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/areas", new { name });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("areaId").GetGuid();
  }

  private async Task<Guid> CreateTableAsync(Guid areaId, string name)
  {
    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/tables",
                                                               new
                                                               {
                                                                 areaId,
                                                                 name,
                                                                 geometry = new
                                                                            {
                                                                              x = 12.5,
                                                                              y = -7.25,
                                                                              width = 80,
                                                                              height = 40.5,
                                                                              rotation = -30
                                                                            }
                                                               });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("tableId").GetGuid();
  }

  private async Task PlaceAsync(Guid stationId, Guid areaId)
  {
    using var response = await _context.Client.PutAsJsonAsync($"{FestivalPath}/stations/{stationId}/shape",
                                                              new
                                                              {
                                                                areaId,
                                                                geometry = new
                                                                           {
                                                                             x = 200.75,
                                                                             y = 30,
                                                                             width = 60,
                                                                             height = 45.5,
                                                                             rotation = -135
                                                                           }
                                                              });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
  }

  private async Task HideAsync(string path)
  {
    using var response = await _context.Client.PostAsync(path, null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
  }

  private async Task<IReadOnlyList<Guid>> PlaceOrderAtAsync(Guid tableId)
  {
    using var response = await _context.PostOrderAsync(new(Guid.NewGuid(),
                                                           tableId,
                                                           [
                                                             new(_context.World.BratwurstItemId, 350, null, null),
                                                             new(_context.World.BratwurstItemId, 350, null, null)
                                                           ]));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("stationOrders").EnumerateArray().SelectMany(stationOrder => stationOrder.GetProperty("itemIds").EnumerateArray()).Select(itemId => itemId.GetGuid()).ToList();
  }

  private async Task HandOutAsync(Guid orderItemId)
  {
    await using var database = _context.Factory.CreateContext();
    var item = await database.OrderItems.SingleAsync(candidate => candidate.Id == orderItemId);

    item.FulfilledAtUtc = DateTime.UtcNow;

    await database.SaveChangesAsync();
  }

  private async Task SettleAsync(IReadOnlyList<Guid> orderItemIds)
  {
    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", new SettleItemsBody(orderItemIds.Select(orderItemId => new SettleLineBody(orderItemId, 350, null)).ToList(), "cash"));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
  }

  private async Task GiveAwayAsync(IReadOnlyList<Guid> orderItemIds)
  {
    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", new SettleItemsBody(orderItemIds.Select(orderItemId => new SettleLineBody(orderItemId, 0, "Kapelle")).ToList(), "none"));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
  }

  private async Task LetTheFestivalEndAsync()
  {
    await using var database = _context.Factory.CreateContext();
    await database.Festivals.Where(festival => festival.Id == _context.World.FestivalId).ExecuteUpdateAsync(festival => festival.SetProperty(entry => entry.EndsAtUtc, DateTime.UtcNow.AddMinutes(-1)));
  }
}
