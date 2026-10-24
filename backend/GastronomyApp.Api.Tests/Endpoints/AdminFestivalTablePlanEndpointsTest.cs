using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminFestivalTablePlanEndpointsTest
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
  public async Task GetTablePlan_AFestivalWithAPlan_AnswersEveryAreaInSortOrderWithItsTablesAndStationShapes()
  {
    var hallId = await CreateAreaAsync("Halle", 2);
    var gardenId = await CreateAreaAsync("Garten", 1);
    var tableId = await CreateTableAsync(gardenId, "Tisch 1");
    await HideAsync($"{FestivalPath}/areas/{hallId}/hide");
    await PlaceAsync(_context.World.KitchenStationId, gardenId);

    using var response = await _context.Client.GetAsync($"{FestivalPath}/table-plan");
    var areas = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("areas");
    var garden = areas[0];
    var table = garden.GetProperty("tables")[0];
    var tableGeometry = table.GetProperty("geometry");
    var stationShape = garden.GetProperty("stationShapes")[0];
    var stationGeometry = stationShape.GetProperty("geometry");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(areas.GetArrayLength(), Is.EqualTo(3));
                      Assert.That(garden.GetProperty("areaId").GetGuid(), Is.EqualTo(gardenId));
                      Assert.That(garden.GetProperty("name").GetString(), Is.EqualTo("Garten"));
                      Assert.That(garden.GetProperty("sortOrder").GetInt32(), Is.EqualTo(1));
                      Assert.That(garden.GetProperty("isHidden").GetBoolean(), Is.False);
                      Assert.That(table.GetProperty("tableId").GetGuid(), Is.EqualTo(tableId));
                      Assert.That(table.GetProperty("name").GetString(), Is.EqualTo("Tisch 1"));
                      Assert.That(table.GetProperty("isHidden").GetBoolean(), Is.False);
                      Assert.That(tableGeometry.GetProperty("x").GetDouble(), Is.EqualTo(12.5));
                      Assert.That(tableGeometry.GetProperty("y").GetDouble(), Is.EqualTo(-7.25));
                      Assert.That(tableGeometry.GetProperty("width").GetDouble(), Is.EqualTo(80));
                      Assert.That(tableGeometry.GetProperty("height").GetDouble(), Is.EqualTo(40.5));
                      Assert.That(tableGeometry.GetProperty("rotation").GetDouble(), Is.EqualTo(-30));
                      Assert.That(stationShape.GetProperty("stationId").GetGuid(), Is.EqualTo(_context.World.KitchenStationId));
                      Assert.That(stationShape.GetProperty("stationName").GetString(), Is.EqualTo("Kueche"));
                      Assert.That(stationGeometry.GetProperty("x").GetDouble(), Is.EqualTo(200.75));
                      Assert.That(stationGeometry.GetProperty("y").GetDouble(), Is.EqualTo(30));
                      Assert.That(stationGeometry.GetProperty("width").GetDouble(), Is.EqualTo(60));
                      Assert.That(stationGeometry.GetProperty("height").GetDouble(), Is.EqualTo(45.5));
                      Assert.That(stationGeometry.GetProperty("rotation").GetDouble(), Is.EqualTo(-135));
                      Assert.That(areas[1].GetProperty("areaId").GetGuid(), Is.EqualTo(hallId));
                      Assert.That(areas[1].GetProperty("isHidden").GetBoolean(), Is.True);
                      Assert.That(areas[1].GetProperty("tables").GetArrayLength(), Is.Zero);
                    });
  }

  [Test]
  public async Task GetTablePlan_AFestivalThatIsNotThere_IsNotFound()
  {
    using var response = await _context.Client.GetAsync($"/api/admin/festivals/{Guid.NewGuid()}/table-plan");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }

  [Test]
  public async Task PostArea_ANameAHiddenAreaCarriesInOtherCapitals_IsRefusedWithTheNameKey()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);
    await HideAsync($"{FestivalPath}/areas/{gardenId}/hide");

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/areas", BuildArea("GARTEN", 2));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("AreaNameTaken"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.festivals.areaNameTaken"));
                    });
  }

  [Test]
  public async Task PostArea_NameMissing_IsRefusedGenerically()
  {
    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/areas", BuildArea("  ", 1));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.actionFailed"));
                    });
  }

  [Test]
  public async Task PutArea_ANewNameAndSortOrder_IsSaved()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);

    using var response = await _context.Client.PutAsJsonAsync($"{FestivalPath}/areas/{gardenId}", BuildArea("Biergarten", 5));

    await using var database = _context.Factory.CreateContext();
    var garden = await database.Areas.SingleAsync(area => area.Id == gardenId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(garden.Name, Is.EqualTo("Biergarten"));
                      Assert.That(garden.SortOrder, Is.EqualTo(5));
                    });
  }

  [Test]
  public async Task PutArea_AnAreaOfAnotherFestival_IsNotFound()
  {
    var strangerAreaId = await CreateAreaAtAnotherFestivalAsync();

    using var response = await _context.Client.PutAsJsonAsync($"{FestivalPath}/areas/{strangerAreaId}", BuildArea("Garten", 1));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }

  [Test]
  public async Task PostAreaHideAndShow_WhileTheFestivalRuns_HideTheAreaAndShowItAgain()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);

    using var hidden = await _context.Client.PostAsync($"{FestivalPath}/areas/{gardenId}/hide", null);
    var isHiddenAfterHiding = await IsAreaHiddenAsync(gardenId);
    using var shown = await _context.Client.PostAsync($"{FestivalPath}/areas/{gardenId}/show", null);
    var isHiddenAfterShowing = await IsAreaHiddenAsync(gardenId);

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(isHiddenAfterHiding, Is.True);
                      Assert.That(shown.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(isHiddenAfterShowing, Is.False);
                    });
  }

  [Test]
  public async Task DeleteArea_WhileTheFestivalRuns_IsRefusedAndKeepsTheArea()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);

    using var response = await _context.Client.DeleteAsync($"{FestivalPath}/areas/{gardenId}");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    await using var database = _context.Factory.CreateContext();
    var areaCount = await database.Areas.CountAsync(area => area.Id == gardenId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("AreaStaysWhileTheFestivalRuns"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.festivals.areaStaysWhileTheFestivalRuns"));
                      Assert.That(areaCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task DeleteArea_AfterTheFestival_RemovesItsTablesAndStationShapesWithIt()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);
    await CreateTableAsync(gardenId, "Tisch 1");
    await CreateTableAsync(gardenId, "Tisch 2");
    await PlaceAsync(_context.World.KitchenStationId, gardenId);
    await LetTheFestivalEndAsync();

    using var response = await _context.Client.DeleteAsync($"{FestivalPath}/areas/{gardenId}");
    var raw = await response.Content.ReadAsStringAsync();

    await using var database = _context.Factory.CreateContext();
    var areaCount = await database.Areas.CountAsync(area => area.Id == gardenId);
    var tableCount = await database.Tables.CountAsync(table => table.AreaId == gardenId);
    var shapeCount = await database.FestivalStationShapes.CountAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent), raw);
                      Assert.That(areaCount, Is.Zero);
                      Assert.That(tableCount, Is.Zero);
                      Assert.That(shapeCount, Is.Zero);
                    });
  }

  [Test]
  public async Task PostTable_AGeometryAsKonvaHandsItOver_IsStoredExactly()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/tables",
                                                               new
                                                               {
                                                                 areaId = gardenId,
                                                                 name = "Tisch 1",
                                                                 geometry = new
                                                                            {
                                                                              x = -0.123456789,
                                                                              y = 9876.54321,
                                                                              width = 0.5,
                                                                              height = 123.456,
                                                                              rotation = -179.75
                                                                            }
                                                               });
    var tableId = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("tableId").GetGuid();

    await using var database = _context.Factory.CreateContext();
    var table = await database.Tables.SingleAsync(candidate => candidate.Id == tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(table.AreaId, Is.EqualTo(gardenId));
                      Assert.That(table.FestivalId, Is.EqualTo(_context.World.FestivalId));
                      Assert.That(table.X, Is.EqualTo(-0.123456789));
                      Assert.That(table.Y, Is.EqualTo(9876.54321));
                      Assert.That(table.Width, Is.EqualTo(0.5));
                      Assert.That(table.Height, Is.EqualTo(123.456));
                      Assert.That(table.Rotation, Is.EqualTo(-179.75));
                      Assert.That(table.IsHidden, Is.False);
                    });
  }

  [Test]
  public async Task PostTable_WithoutAGeometry_IsRefusedGenerically()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/tables",
                                                               new
                                                               {
                                                                 areaId = gardenId,
                                                                 name = "Tisch 1",
                                                                 geometry = (object?)null
                                                               });
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.actionFailed"));
                    });
  }

  [Test]
  public async Task PostTable_ANameAHiddenTableInAnotherAreaCarriesInOtherCapitals_IsRefusedWithTheNameKey()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);
    var hallId = await CreateAreaAsync("Halle", 2);
    var tableId = await CreateTableAsync(gardenId, "Tisch 4");
    await HideAsync($"{FestivalPath}/tables/{tableId}/hide");

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/tables", BuildTable(hallId, "tisch 4"));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("TableNameTaken"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.festivals.tableNameTaken"));
                    });
  }

  [Test]
  public async Task PostTable_InAnAreaOfAnotherFestival_IsRefusedGenerically()
  {
    var strangerAreaId = await CreateAreaAtAnotherFestivalAsync();

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/tables", BuildTable(strangerAreaId, "Tisch 1"));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.actionFailed"));
                    });
  }

  [Test]
  public async Task PutTable_AnotherAreaOfTheFestival_MovesTheTableThere()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);
    var hallId = await CreateAreaAsync("Halle", 2);
    var tableId = await CreateTableAsync(gardenId, "Tisch 1");

    using var response = await _context.Client.PutAsJsonAsync($"{FestivalPath}/tables/{tableId}",
                                                              new
                                                              {
                                                                areaId = hallId,
                                                                name = "Stammtisch",
                                                                geometry = new
                                                                           {
                                                                             x = 3.5,
                                                                             y = 4.5,
                                                                             width = 120,
                                                                             height = 60,
                                                                             rotation = 90
                                                                           }
                                                              });

    await using var database = _context.Factory.CreateContext();
    var table = await database.Tables.SingleAsync(candidate => candidate.Id == tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(table.AreaId, Is.EqualTo(hallId));
                      Assert.That(table.Name, Is.EqualTo("Stammtisch"));
                      Assert.That(table.X, Is.EqualTo(3.5));
                      Assert.That(table.Rotation, Is.EqualTo(90));
                    });
  }

  [Test]
  public async Task PutTable_AnAreaOfAnotherFestival_IsRefusedGenericallyAndLeavesTheTableWhereItWas()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);
    var tableId = await CreateTableAsync(gardenId, "Tisch 1");
    var strangerAreaId = await CreateAreaAtAnotherFestivalAsync();

    using var response = await _context.Client.PutAsJsonAsync($"{FestivalPath}/tables/{tableId}", BuildTable(strangerAreaId, "Tisch 1"));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    await using var database = _context.Factory.CreateContext();
    var table = await database.Tables.SingleAsync(candidate => candidate.Id == tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(table.AreaId, Is.EqualTo(gardenId));
                    });
  }

  [Test]
  public async Task PostTableHideAndShow_WhileTheFestivalRuns_HideTheTableAndShowItAgain()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);
    var tableId = await CreateTableAsync(gardenId, "Tisch 1");

    using var hidden = await _context.Client.PostAsync($"{FestivalPath}/tables/{tableId}/hide", null);
    var isHiddenAfterHiding = await IsTableHiddenAsync(tableId);
    using var shown = await _context.Client.PostAsync($"{FestivalPath}/tables/{tableId}/show", null);
    var isHiddenAfterShowing = await IsTableHiddenAsync(tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(isHiddenAfterHiding, Is.True);
                      Assert.That(shown.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(isHiddenAfterShowing, Is.False);
                    });
  }

  [Test]
  public async Task PostTableHide_ATableWithAnItemNotYetHandedOutWhileTheFestivalRuns_IsRefusedAndKeepsItShown()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);
    var tableId = await CreateTableAsync(gardenId, "Tisch 1");
    await PlaceOrderAtAsync(tableId);

    using var response = await _context.Client.PostAsync($"{FestivalPath}/tables/{tableId}/hide", null);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    var isHidden = await IsTableHiddenAsync(tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("TableWithItemsToHandOutOrSettleStaysShown"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.festivals.tableWithItemsToHandOutOrSettleStaysShown"));
                      Assert.That(isHidden, Is.False);
                    });
  }

  [Test]
  public async Task PostTableHide_ATableWithItemsToHandOutOrSettleAfterTheFestival_HidesIt()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);
    var tableId = await CreateTableAsync(gardenId, "Tisch 1");
    await PlaceOrderAtAsync(tableId);
    await LetTheFestivalEndAsync();

    using var response = await _context.Client.PostAsync($"{FestivalPath}/tables/{tableId}/hide", null);
    var isHidden = await IsTableHiddenAsync(tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(isHidden, Is.True);
                    });
  }

  [Test]
  public async Task PostAreaHide_AnAreaWhoseTableHasAnItemNotYetSettledWhileTheFestivalRuns_IsRefusedAndKeepsItShown()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);
    var tableId = await CreateTableAsync(gardenId, "Tisch 1");
    await PlaceOrderAtAsync(tableId);

    using var response = await _context.Client.PostAsync($"{FestivalPath}/areas/{gardenId}/hide", null);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    var isHidden = await IsAreaHiddenAsync(gardenId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("AreaWithItemsToHandOutOrSettleStaysShown"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.festivals.areaWithItemsToHandOutOrSettleStaysShown"));
                      Assert.That(isHidden, Is.False);
                    });
  }

  [Test]
  public async Task DeleteTable_ATableWithAnOrderAfterTheFestival_IsRefusedAndKeepsTheTable()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);
    var tableId = await CreateTableAsync(gardenId, "Tisch 1");
    await PlaceOrderAtAsync(tableId);
    await LetTheFestivalEndAsync();

    using var response = await _context.Client.DeleteAsync($"{FestivalPath}/tables/{tableId}");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    await using var database = _context.Factory.CreateContext();
    var tableCount = await database.Tables.CountAsync(table => table.Id == tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("TableWithOrdersStays"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.festivals.tableWithOrdersStays"));
                      Assert.That(tableCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task DeleteArea_AnAreaWhoseTableHasAnOrderAfterTheFestival_IsRefusedAndKeepsTheAreaAndItsTables()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);
    var tableId = await CreateTableAsync(gardenId, "Tisch 1");
    await CreateTableAsync(gardenId, "Tisch 2");
    await PlaceOrderAtAsync(tableId);
    await LetTheFestivalEndAsync();

    using var response = await _context.Client.DeleteAsync($"{FestivalPath}/areas/{gardenId}");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    await using var database = _context.Factory.CreateContext();
    var areaCount = await database.Areas.CountAsync(area => area.Id == gardenId);
    var tableCount = await database.Tables.CountAsync(table => table.AreaId == gardenId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("AreaWithOrdersStays"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.festivals.areaWithOrdersStays"));
                      Assert.That(areaCount, Is.EqualTo(1));
                      Assert.That(tableCount, Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task DeleteTable_WhileTheFestivalRuns_IsRefusedAndKeepsTheTable()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);
    var tableId = await CreateTableAsync(gardenId, "Tisch 1");

    using var response = await _context.Client.DeleteAsync($"{FestivalPath}/tables/{tableId}");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    await using var database = _context.Factory.CreateContext();
    var tableCount = await database.Tables.CountAsync(table => table.Id == tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("TableStaysWhileTheFestivalRuns"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.festivals.tableStaysWhileTheFestivalRuns"));
                      Assert.That(tableCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task DeleteTable_AfterTheFestival_RemovesTheTable()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);
    var tableId = await CreateTableAsync(gardenId, "Tisch 1");
    await LetTheFestivalEndAsync();

    using var response = await _context.Client.DeleteAsync($"{FestivalPath}/tables/{tableId}");

    await using var database = _context.Factory.CreateContext();
    var tableCount = await database.Tables.CountAsync(table => table.Id == tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
                      Assert.That(tableCount, Is.Zero);
                    });
  }

  [Test]
  public async Task PutStationShape_AStationAtTheFestival_PlacesItAndASecondCallMovesTheSameShape()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);
    var hallId = await CreateAreaAsync("Halle", 2);
    await PlaceAsync(_context.World.BarStationId, gardenId);

    using var response = await _context.Client.PutAsJsonAsync($"{FestivalPath}/stations/{_context.World.BarStationId}/shape",
                                                              new
                                                              {
                                                                areaId = hallId,
                                                                geometry = new
                                                                           {
                                                                             x = 5,
                                                                             y = 6,
                                                                             width = 70,
                                                                             height = 35,
                                                                             rotation = 45.5
                                                                           }
                                                              });
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    await using var database = _context.Factory.CreateContext();
    var shape = await database.FestivalStationShapes.Include(candidate => candidate.FestivalStation).SingleAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.GetProperty("stationId").GetGuid(), Is.EqualTo(_context.World.BarStationId));
                      Assert.That(shape.FestivalStation.StationId, Is.EqualTo(_context.World.BarStationId));
                      Assert.That(shape.AreaId, Is.EqualTo(hallId));
                      Assert.That(shape.X, Is.EqualTo(5));
                      Assert.That(shape.Y, Is.EqualTo(6));
                      Assert.That(shape.Width, Is.EqualTo(70));
                      Assert.That(shape.Height, Is.EqualTo(35));
                      Assert.That(shape.Rotation, Is.EqualTo(45.5));
                    });
  }

  [Test]
  public async Task PutStationShape_AStationThatDoesNotTakePartInTheFestival_IsNotFound()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);

    using var response = await _context.Client.PutAsJsonAsync($"{FestivalPath}/stations/{Guid.NewGuid()}/shape", BuildStationShape(gardenId));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }

  [Test]
  public async Task PutStationShape_AnAreaOfAnotherFestival_IsRefusedGenerically()
  {
    var strangerAreaId = await CreateAreaAtAnotherFestivalAsync();

    using var response = await _context.Client.PutAsJsonAsync($"{FestivalPath}/stations/{_context.World.KitchenStationId}/shape", BuildStationShape(strangerAreaId));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.actionFailed"));
                    });
  }

  [Test]
  public async Task DeleteStationShape_AStationOnThePlan_TakesItOffThePlanAndKeepsItAtTheFestival()
  {
    var gardenId = await CreateAreaAsync("Garten", 1);
    await PlaceAsync(_context.World.KitchenStationId, gardenId);

    using var response = await _context.Client.DeleteAsync($"{FestivalPath}/stations/{_context.World.KitchenStationId}/shape");

    await using var database = _context.Factory.CreateContext();
    var shapeCount = await database.FestivalStationShapes.CountAsync();
    var linkCount = await database.FestivalStations.CountAsync(link => link.FestivalId == _context.World.FestivalId && link.StationId == _context.World.KitchenStationId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
                      Assert.That(shapeCount, Is.Zero);
                      Assert.That(linkCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task DeleteStationShape_AStationNotOnThePlan_IsNotFound()
  {
    using var response = await _context.Client.DeleteAsync($"{FestivalPath}/stations/{_context.World.KitchenStationId}/shape");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }

  private object BuildArea(string name, int sortOrder)
  {
    return new
           {
             name,
             sortOrder
           };
  }

  private object BuildTable(Guid areaId, string name)
  {
    return new
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
           };
  }

  private object BuildStationShape(Guid areaId)
  {
    return new
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
           };
  }

  private async Task<Guid> CreateAreaAsync(string name, int sortOrder)
  {
    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/areas", BuildArea(name, sortOrder));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("areaId").GetGuid();
  }

  private async Task<Guid> CreateTableAsync(Guid areaId, string name)
  {
    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/tables", BuildTable(areaId, name));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("tableId").GetGuid();
  }

  private async Task PlaceAsync(Guid stationId, Guid areaId)
  {
    using var response = await _context.Client.PutAsJsonAsync($"{FestivalPath}/stations/{stationId}/shape", BuildStationShape(areaId));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
  }

  private async Task PlaceOrderAtAsync(Guid tableId)
  {
    using var response = await _context.PostOrderAsync(new(Guid.NewGuid(), tableId, [new(_context.World.BratwurstItemId, 350, null, null)]));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
  }

  private async Task HideAsync(string path)
  {
    using var response = await _context.Client.PostAsync(path, null);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
  }

  private async Task<Guid> CreateAreaAtAnotherFestivalAsync()
  {
    var startsAtUtc = DateTime.UtcNow.AddYears(2);

    using var created = await _context.Client.PostAsJsonAsync("/api/admin/festivals",
                                                              new
                                                              {
                                                                name = "Herbstfest",
                                                                startsAtUtc = startsAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                                                endsAtUtc = startsAtUtc.AddHours(24).ToString("yyyy-MM-ddTHH:mm:ssZ")
                                                              });

    Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var otherFestivalId = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("festivalId").GetGuid();

    using var response = await _context.Client.PostAsJsonAsync($"/api/admin/festivals/{otherFestivalId}/areas", BuildArea("Garten", 1));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("areaId").GetGuid();
  }

  private async Task<bool> IsAreaHiddenAsync(Guid areaId)
  {
    await using var database = _context.Factory.CreateContext();

    return (await database.Areas.SingleAsync(area => area.Id == areaId)).IsHidden;
  }

  private async Task<bool> IsTableHiddenAsync(Guid tableId)
  {
    await using var database = _context.Factory.CreateContext();

    return (await database.Tables.SingleAsync(table => table.Id == tableId)).IsHidden;
  }

  private async Task LetTheFestivalEndAsync()
  {
    await using var database = _context.Factory.CreateContext();
    await database.Festivals.Where(festival => festival.Id == _context.World.FestivalId).ExecuteUpdateAsync(festival => festival.SetProperty(entry => entry.EndsAtUtc, DateTime.UtcNow.AddMinutes(-1)));
  }
}
