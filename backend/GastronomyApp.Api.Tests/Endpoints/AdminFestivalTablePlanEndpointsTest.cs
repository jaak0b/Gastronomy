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

  private const string SeededAreaName = "Festzelt";

  private OrderTestContext _context = null!;

  private string FestivalPath => $"/api/admin/festivals/{_context.World.FestivalId}";

  [Test]
  public async Task GetTablePlan_AFestivalWithAPlan_AnswersEveryAreaInSortOrderWithItsTablesAndStationShapes()
  {
    var gardenId = await CreateAreaAsync("Garten");
    var hallId = await CreateAreaAsync("Halle");
    var tableId = await CreateTableAsync(gardenId, "Tisch 1");
    await HideAsync($"{FestivalPath}/areas/{hallId}/hide");
    await PlaceAsync(_context.World.KitchenStationId, gardenId);

    using var response = await _context.Client.GetAsync($"{FestivalPath}/table-plan");
    var areas = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("areas");
    var garden = areas[1];
    var table = garden.GetProperty("tables")[0];
    var tableGeometry = table.GetProperty("geometry");
    var stationShape = garden.GetProperty("stationShapes")[0];
    var stationGeometry = stationShape.GetProperty("geometry");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(areas.GetArrayLength(), Is.EqualTo(3));
                      Assert.That(areas[0].GetProperty("name").GetString(), Is.EqualTo(SeededAreaName));
                      Assert.That(garden.GetProperty("areaId").GetGuid(), Is.EqualTo(gardenId));
                      Assert.That(garden.GetProperty("name").GetString(), Is.EqualTo("Garten"));
                      Assert.That(garden.GetProperty("isHidden").GetBoolean(), Is.False);
                      Assert.That(garden.GetProperty("hasItemsToHandOutOrSettleAtTheRunningFestival").GetBoolean(), Is.False);
                      Assert.That(table.GetProperty("tableId").GetGuid(), Is.EqualTo(tableId));
                      Assert.That(table.GetProperty("name").GetString(), Is.EqualTo("Tisch 1"));
                      Assert.That(table.GetProperty("isHidden").GetBoolean(), Is.False);
                      Assert.That(table.GetProperty("hasItemsToHandOutOrSettleAtTheRunningFestival").GetBoolean(), Is.False);
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
                      Assert.That(areas[2].GetProperty("areaId").GetGuid(), Is.EqualTo(hallId));
                      Assert.That(areas[2].GetProperty("isHidden").GetBoolean(), Is.True);
                      Assert.That(areas[2].GetProperty("tables").GetArrayLength(), Is.Zero);
                    });
  }

  [Test]
  public async Task GetTablePlan_ATableWithAnItemNotYetHandedOut_SaysSoForThatTableAndItsArea()
  {
    var gardenId = await CreateAreaAsync("Garten");
    var orderedAtId = await CreateTableAsync(gardenId, "Tisch 1");
    await CreateTableAsync(gardenId, "Tisch 2");
    await PlaceOrderAtAsync(orderedAtId);

    using var response = await _context.Client.GetAsync($"{FestivalPath}/table-plan");
    var areas = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("areas");
    var garden = areas.EnumerateArray().Single(area => area.GetProperty("areaId").GetGuid() == gardenId);
    var tables = garden.GetProperty("tables");

    Assert.Multiple(() =>
                    {
                      Assert.That(garden.GetProperty("hasItemsToHandOutOrSettleAtTheRunningFestival").GetBoolean(), Is.True);
                      Assert.That(tables[0].GetProperty("hasItemsToHandOutOrSettleAtTheRunningFestival").GetBoolean(), Is.True);
                      Assert.That(tables[1].GetProperty("hasItemsToHandOutOrSettleAtTheRunningFestival").GetBoolean(), Is.False);
                    });
  }

  [TestCase(-1, 5, true, HttpStatusCode.Conflict)]
  [TestCase(24, 48, false, HttpStatusCode.OK)]
  [TestCase(-48, -24, false, HttpStatusCode.OK)]
  public async Task GetTablePlanThenHideTheTable_ATableWithAnItemNotYetHandedOut_ThePlanSaysWhatTheLaptopAnswers(int startsInHours, int endsInHours, bool isListedWithItemsLeft, HttpStatusCode answer)
  {
    var gardenId = await CreateAreaAsync("Garten");
    var tableId = await CreateTableAsync(gardenId, "Tisch 1");
    await PlaceOrderAtAsync(tableId);
    await MoveTheFestivalAsync(startsInHours, endsInHours);

    var garden = await ListedAreaAsync(gardenId);
    var table = garden.GetProperty("tables").EnumerateArray().Single(candidate => candidate.GetProperty("tableId").GetGuid() == tableId);

    using var response = await _context.Client.PostAsync($"{FestivalPath}/tables/{tableId}/hide", null);

    Assert.Multiple(() =>
                    {
                      Assert.That(table.GetProperty("hasItemsToHandOutOrSettleAtTheRunningFestival").GetBoolean(), Is.EqualTo(isListedWithItemsLeft));
                      Assert.That(response.StatusCode, Is.EqualTo(answer));
                    });
  }

  [TestCase(-1, 5, true, HttpStatusCode.Conflict)]
  [TestCase(24, 48, false, HttpStatusCode.OK)]
  [TestCase(-48, -24, false, HttpStatusCode.OK)]
  public async Task GetTablePlanThenHideTheArea_AnAreaWhoseTableHasAnItemNotYetHandedOut_ThePlanSaysWhatTheLaptopAnswers(int startsInHours, int endsInHours, bool isListedWithItemsLeft, HttpStatusCode answer)
  {
    var gardenId = await CreateAreaAsync("Garten");
    var tableId = await CreateTableAsync(gardenId, "Tisch 1");
    await PlaceOrderAtAsync(tableId);
    await MoveTheFestivalAsync(startsInHours, endsInHours);

    var garden = await ListedAreaAsync(gardenId);

    using var response = await _context.Client.PostAsync($"{FestivalPath}/areas/{gardenId}/hide", null);

    Assert.Multiple(() =>
                    {
                      Assert.That(garden.GetProperty("hasItemsToHandOutOrSettleAtTheRunningFestival").GetBoolean(), Is.EqualTo(isListedWithItemsLeft));
                      Assert.That(response.StatusCode, Is.EqualTo(answer));
                    });
  }

  [Test]
  public async Task PostAreaMove_AnAreaUp_PutsItBeforeTheOneAbove()
  {
    var gardenId = await CreateAreaAsync("Garten");
    var hallId = await CreateAreaAsync("Halle");

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/areas/{hallId}/move",
                                                               new
                                                               {
                                                                 direction = "up",
                                                                 showsHiddenAreas = false
                                                               });

    await using var database = _context.Factory.CreateContext();
    var garden = await database.Areas.SingleAsync(area => area.Id == gardenId);
    var hall = await database.Areas.SingleAsync(area => area.Id == hallId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(hall.SortOrder, Is.LessThan(garden.SortOrder));
                    });
  }

  [Test]
  public async Task PostAreaMove_AHiddenAreaBetweenWhileHiddenAreasAreNotShown_PassesOverIt()
  {
    await CreateAreaAsync("Garten");
    var hallId = await CreateAreaAsync("Halle");
    var tentId = await CreateAreaAsync("Zelt");
    await HideAsync($"{FestivalPath}/areas/{hallId}/hide");

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/areas/{tentId}/move",
                                                               new
                                                               {
                                                                 direction = "up",
                                                                 showsHiddenAreas = false
                                                               });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    Assert.That(await AreaNamesInOrderAsync(),
                Is.EqualTo(new[]
                           {
                             SeededAreaName,
                             "Zelt",
                             "Halle",
                             "Garten"
                           }));
  }

  [Test]
  public async Task PostAreaMove_AHiddenAreaBetweenWhileHiddenAreasAreShown_SwapsWithIt()
  {
    await CreateAreaAsync("Garten");
    var hallId = await CreateAreaAsync("Halle");
    var tentId = await CreateAreaAsync("Zelt");
    await HideAsync($"{FestivalPath}/areas/{hallId}/hide");

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/areas/{tentId}/move",
                                                               new
                                                               {
                                                                 direction = "up",
                                                                 showsHiddenAreas = true
                                                               });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    Assert.That(await AreaNamesInOrderAsync(),
                Is.EqualTo(new[]
                           {
                             SeededAreaName,
                             "Garten",
                             "Zelt",
                             "Halle"
                           }));
  }

  [Test]
  public async Task PostAreaMove_AnAreaOfAnotherFestival_IsNotFound()
  {
    var strangerAreaId = await CreateAreaAtAnotherFestivalAsync();

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/areas/{strangerAreaId}/move",
                                                               new
                                                               {
                                                                 direction = "down",
                                                                 showsHiddenAreas = false
                                                               });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
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
    var gardenId = await CreateAreaAsync("Garten");
    await HideAsync($"{FestivalPath}/areas/{gardenId}/hide");

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/areas", BuildArea("GARTEN"));
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
    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/areas", BuildArea("  "));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("common.errors.actionFailed"));
                    });
  }

  [Test]
  public async Task PostArea_AFestivalWhoseLastAreaIsHidden_NumbersTheNewAreaAfterIt()
  {
    var hallId = await CreateAreaAsync("Halle");
    await HideAsync($"{FestivalPath}/areas/{hallId}/hide");

    var gardenId = await CreateAreaAsync("Garten");

    await using var database = _context.Factory.CreateContext();
    var hall = await database.Areas.SingleAsync(area => area.Id == hallId);
    var garden = await database.Areas.SingleAsync(area => area.Id == gardenId);

    Assert.That(garden.SortOrder, Is.EqualTo(hall.SortOrder + 1));
  }

  [Test]
  public async Task PutArea_ANewName_IsSavedAndTheAreaKeepsItsPlace()
  {
    await CreateAreaAsync("Garten");
    var hallId = await CreateAreaAsync("Halle");

    using var response = await _context.Client.PutAsJsonAsync($"{FestivalPath}/areas/{hallId}", BuildArea("Alm"));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    Assert.That(await AreaNamesInOrderAsync(),
                Is.EqualTo(new[]
                           {
                             SeededAreaName,
                             "Garten",
                             "Alm"
                           }));
  }

  [Test]
  public async Task PutArea_AnAreaOfAnotherFestival_IsNotFound()
  {
    var strangerAreaId = await CreateAreaAtAnotherFestivalAsync();

    using var response = await _context.Client.PutAsJsonAsync($"{FestivalPath}/areas/{strangerAreaId}", BuildArea("Garten"));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }

  [Test]
  public async Task PostAreaHideAndShow_WhileTheFestivalRuns_HideTheAreaAndShowItAgain()
  {
    var gardenId = await CreateAreaAsync("Garten");

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
  public async Task PostTable_AGeometryAsKonvaHandsItOver_IsStoredExactly()
  {
    var gardenId = await CreateAreaAsync("Garten");

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
    var gardenId = await CreateAreaAsync("Garten");

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
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("common.errors.actionFailed"));
                    });
  }

  [Test]
  public async Task PostTable_ANameAHiddenTableInAnotherAreaCarriesInOtherCapitals_IsRefusedWithTheNameKey()
  {
    var gardenId = await CreateAreaAsync("Garten");
    var hallId = await CreateAreaAsync("Halle");
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
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("common.errors.actionFailed"));
                    });
  }

  [Test]
  public async Task PutTable_AnotherAreaOfTheFestival_MovesTheTableThere()
  {
    var gardenId = await CreateAreaAsync("Garten");
    var hallId = await CreateAreaAsync("Halle");
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
    var gardenId = await CreateAreaAsync("Garten");
    var tableId = await CreateTableAsync(gardenId, "Tisch 1");
    var strangerAreaId = await CreateAreaAtAnotherFestivalAsync();

    using var response = await _context.Client.PutAsJsonAsync($"{FestivalPath}/tables/{tableId}", BuildTable(strangerAreaId, "Tisch 1"));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    await using var database = _context.Factory.CreateContext();
    var table = await database.Tables.SingleAsync(candidate => candidate.Id == tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("common.errors.actionFailed"));
                      Assert.That(table.AreaId, Is.EqualTo(gardenId));
                    });
  }

  [Test]
  public async Task PostTableHideAndShow_WhileTheFestivalRuns_HideTheTableAndShowItAgain()
  {
    var gardenId = await CreateAreaAsync("Garten");
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
    var gardenId = await CreateAreaAsync("Garten");
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
    var gardenId = await CreateAreaAsync("Garten");
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
    var gardenId = await CreateAreaAsync("Garten");
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
  public async Task PutStationShape_AStationAtTheFestival_PlacesItAndASecondCallMovesTheSameShape()
  {
    var gardenId = await CreateAreaAsync("Garten");
    var hallId = await CreateAreaAsync("Halle");
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
    var gardenId = await CreateAreaAsync("Garten");

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
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("common.errors.actionFailed"));
                    });
  }

  [Test]
  public async Task DeleteStationShape_AStationOnThePlan_TakesItOffThePlanAndKeepsItAtTheFestival()
  {
    var gardenId = await CreateAreaAsync("Garten");
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

  [Test]
  public async Task PostTablesCopy_TwoTablesOfTheFestival_CreatesBothWithTheNextFreeNamesAndAnswersTheirIds()
  {
    var gardenId = await CreateAreaAsync("Garten");
    var hallId = await CreateAreaAsync("Halle");
    var tableA1Id = await CreateTableAsync(gardenId, "A1");
    var tableA2Id = await CreateTableAsync(gardenId, "A2");

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/tables/copy",
                                                               new
                                                               {
                                                                 areaId = hallId,
                                                                 tables = new[]
                                                                          {
                                                                            new
                                                                            {
                                                                              sourceTableId = tableA2Id,
                                                                              geometry = new
                                                                                         {
                                                                                           x = 100.25,
                                                                                           y = 200.5,
                                                                                           width = 90.75,
                                                                                           height = 45.25,
                                                                                           rotation = -30.5
                                                                                         }
                                                                            },
                                                                            new
                                                                            {
                                                                              sourceTableId = tableA1Id,
                                                                              geometry = new
                                                                                         {
                                                                                           x = 10.125,
                                                                                           y = 20.375,
                                                                                           width = 80.5,
                                                                                           height = 40.25,
                                                                                           rotation = 15.75
                                                                                         }
                                                                            }
                                                                          }
                                                               });
    var tableIds = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("tableIds").EnumerateArray().Select(id => id.GetGuid()).ToList();

    await using var database = _context.Factory.CreateContext();
    var copies = await database.Tables.Where(table => tableIds.Contains(table.Id)).OrderBy(table => table.Name).ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                      Assert.That(tableIds, Has.Count.EqualTo(2));
                      Assert.That(copies.Select(table => table.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "A3",
                                               "A4"
                                             }));
                      Assert.That(copies.Select(table => table.AreaId), Is.All.EqualTo(hallId));
                      Assert.That(copies.Select(table => table.FestivalId), Is.All.EqualTo(_context.World.FestivalId));
                      Assert.That(copies.Select(table => table.IsHidden), Is.All.False);
                      Assert.That(copies.Single(table => table.Name == "A3").X, Is.EqualTo(10.125));
                      Assert.That(copies.Single(table => table.Name == "A3").Y, Is.EqualTo(20.375));
                      Assert.That(copies.Single(table => table.Name == "A3").Width, Is.EqualTo(80.5));
                      Assert.That(copies.Single(table => table.Name == "A3").Height, Is.EqualTo(40.25));
                      Assert.That(copies.Single(table => table.Name == "A3").Rotation, Is.EqualTo(15.75));
                      Assert.That(copies.Single(table => table.Name == "A4").X, Is.EqualTo(100.25));
                      Assert.That(copies.Single(table => table.Name == "A4").Rotation, Is.EqualTo(-30.5));
                    });
  }

  [Test]
  public async Task PostTablesCopy_ASecondSourceTheFestivalDoesNotHave_CreatesNoTable()
  {
    var gardenId = await CreateAreaAsync("Garten");
    var tableId = await CreateTableAsync(gardenId, "A1");

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/tables/copy",
                                                               new
                                                               {
                                                                 areaId = gardenId,
                                                                 tables = new[]
                                                                          {
                                                                            new
                                                                            {
                                                                              sourceTableId = tableId,
                                                                              geometry = new
                                                                                         {
                                                                                           x = 50,
                                                                                           y = 60,
                                                                                           width = 80,
                                                                                           height = 40,
                                                                                           rotation = 0
                                                                                         }
                                                                            },
                                                                            new
                                                                            {
                                                                              sourceTableId = Guid.NewGuid(),
                                                                              geometry = new
                                                                                         {
                                                                                           x = 50,
                                                                                           y = 120,
                                                                                           width = 80,
                                                                                           height = 40,
                                                                                           rotation = 0
                                                                                         }
                                                                            }
                                                                          }
                                                               });

    await using var database = _context.Factory.CreateContext();
    var tableCount = await database.Tables.CountAsync(table => table.FestivalId == _context.World.FestivalId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                      Assert.That(tableCount, Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task PostTablesCopy_AGeneratedNameLongerThanFortyCharacters_IsRefusedAndCreatesNoTable()
  {
    var gardenId = await CreateAreaAsync("Garten");
    var tableId = await CreateTableAsync(gardenId, new string('T', 40));

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/tables/copy",
                                                               new
                                                               {
                                                                 areaId = gardenId,
                                                                 tables = new[]
                                                                          {
                                                                            new
                                                                            {
                                                                              sourceTableId = tableId,
                                                                              geometry = new
                                                                                         {
                                                                                           x = 50,
                                                                                           y = 60,
                                                                                           width = 80,
                                                                                           height = 40,
                                                                                           rotation = 0
                                                                                         }
                                                                            }
                                                                          }
                                                               });
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    await using var database = _context.Factory.CreateContext();
    var tableCount = await database.Tables.CountAsync(table => table.FestivalId == _context.World.FestivalId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("TableCopyNameTooLong"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.admin.festivals.tableCopyNameTooLong"));
                      Assert.That(tableCount, Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task PostTablesCopy_AnAreaOfAnotherFestival_IsRefusedGenericallyAndCreatesNoTable()
  {
    var gardenId = await CreateAreaAsync("Garten");
    var tableId = await CreateTableAsync(gardenId, "A1");
    var strangerAreaId = await CreateAreaAtAnotherFestivalAsync();

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/tables/copy",
                                                               new
                                                               {
                                                                 areaId = strangerAreaId,
                                                                 tables = new[]
                                                                          {
                                                                            new
                                                                            {
                                                                              sourceTableId = tableId,
                                                                              geometry = new
                                                                                         {
                                                                                           x = 50,
                                                                                           y = 60,
                                                                                           width = 80,
                                                                                           height = 40,
                                                                                           rotation = 0
                                                                                         }
                                                                            }
                                                                          }
                                                               });
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    await using var database = _context.Factory.CreateContext();
    var tableCount = await database.Tables.CountAsync(table => table.FestivalId == _context.World.FestivalId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("common.errors.actionFailed"));
                      Assert.That(tableCount, Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task PostTablesCopy_NoTable_IsRefusedGenerically()
  {
    var gardenId = await CreateAreaAsync("Garten");

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/tables/copy",
                                                               new
                                                               {
                                                                 areaId = gardenId,
                                                                 tables = Array.Empty<object>()
                                                               });
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("common.errors.actionFailed"));
                      Assert.That(body.GetProperty("parameters").TryGetProperty("member", out _), Is.False);
                    });
  }

  [Test]
  public async Task PostTablesCopy_TheSameSourceTableTwice_IsRefusedGenericallyAndCreatesNoTable()
  {
    var gardenId = await CreateAreaAsync("Garten");
    var tableId = await CreateTableAsync(gardenId, "A1");

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/tables/copy",
                                                               new
                                                               {
                                                                 areaId = gardenId,
                                                                 tables = new[]
                                                                          {
                                                                            BuildCopySource(tableId, 50, 60, 0),
                                                                            BuildCopySource(tableId, 50, 120, 0)
                                                                          }
                                                               });
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    await using var database = _context.Factory.CreateContext();
    var tableCount = await database.Tables.CountAsync(table => table.FestivalId == _context.World.FestivalId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("common.errors.actionFailed"));
                      Assert.That(body.GetProperty("parameters").TryGetProperty("member", out _), Is.False);
                      Assert.That(tableCount, Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task PostTablesMove_TwoTablesOfTheFestival_MovesBothAndAnswersNoContent()
  {
    var gardenId = await CreateAreaAsync("Garten");
    var tableA1Id = await CreateTableAsync(gardenId, "A1");
    var tableA2Id = await CreateTableAsync(gardenId, "A2");

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/tables/move",
                                                               new
                                                               {
                                                                 tables = new[]
                                                                          {
                                                                            BuildMoveTable(tableA1Id, 110.125, 210.375, -30.5),
                                                                            BuildMoveTable(tableA2Id, 120.25, 220.5, 45.75)
                                                                          }
                                                               });

    await using var database = _context.Factory.CreateContext();
    var tableA1 = await database.Tables.SingleAsync(table => table.Id == tableA1Id);
    var tableA2 = await database.Tables.SingleAsync(table => table.Id == tableA2Id);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
                      Assert.That(tableA1.X, Is.EqualTo(110.125));
                      Assert.That(tableA1.Y, Is.EqualTo(210.375));
                      Assert.That(tableA1.Width, Is.EqualTo(80));
                      Assert.That(tableA1.Height, Is.EqualTo(40));
                      Assert.That(tableA1.Rotation, Is.EqualTo(-30.5));
                      Assert.That(tableA1.Name, Is.EqualTo("A1"));
                      Assert.That(tableA1.AreaId, Is.EqualTo(gardenId));
                      Assert.That(tableA2.X, Is.EqualTo(120.25));
                      Assert.That(tableA2.Y, Is.EqualTo(220.5));
                      Assert.That(tableA2.Rotation, Is.EqualTo(45.75));
                      Assert.That(tableA2.Name, Is.EqualTo("A2"));
                    });
  }

  [Test]
  public async Task PostTablesMove_ASecondTableTheFestivalDoesNotHave_IsNotFoundAndMovesNothing()
  {
    var gardenId = await CreateAreaAsync("Garten");
    var tableId = await CreateTableAsync(gardenId, "A1");

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/tables/move",
                                                               new
                                                               {
                                                                 tables = new[]
                                                                          {
                                                                            BuildMoveTable(tableId, 50, 60, 0),
                                                                            BuildMoveTable(Guid.NewGuid(), 50, 120, 0)
                                                                          }
                                                               });

    await using var database = _context.Factory.CreateContext();
    var table = await database.Tables.SingleAsync(candidate => candidate.Id == tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                      Assert.That(table.X, Is.EqualTo(12.5));
                      Assert.That(table.Y, Is.EqualTo(-7.25));
                    });
  }

  [Test]
  public async Task PostTablesMove_NoTable_IsRefusedGenerically()
  {
    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/tables/move", new { tables = Array.Empty<object>() });
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("common.errors.actionFailed"));
                      Assert.That(body.GetProperty("parameters").TryGetProperty("member", out _), Is.False);
                    });
  }

  [Test]
  public async Task PostTablesMove_TheSameTableTwice_IsRefusedGenericallyAndMovesNothing()
  {
    var gardenId = await CreateAreaAsync("Garten");
    var tableId = await CreateTableAsync(gardenId, "A1");

    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/tables/move",
                                                               new
                                                               {
                                                                 tables = new[]
                                                                          {
                                                                            BuildMoveTable(tableId, 50, 60, 0),
                                                                            BuildMoveTable(tableId, 50, 120, 0)
                                                                          }
                                                               });
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    await using var database = _context.Factory.CreateContext();
    var table = await database.Tables.SingleAsync(candidate => candidate.Id == tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("common.errors.actionFailed"));
                      Assert.That(body.GetProperty("parameters").TryGetProperty("member", out _), Is.False);
                      Assert.That(table.X, Is.EqualTo(12.5));
                      Assert.That(table.Y, Is.EqualTo(-7.25));
                    });
  }

  private object BuildCopySource(Guid sourceTableId, double x, double y, double rotation)
  {
    return new
           {
             sourceTableId,
             geometry = new
                        {
                          x,
                          y,
                          width = 80,
                          height = 40,
                          rotation
                        }
           };
  }

  private object BuildMoveTable(Guid tableId, double x, double y, double rotation)
  {
    return new
           {
             tableId,
             geometry = new
                        {
                          x,
                          y,
                          width = 80,
                          height = 40,
                          rotation
                        }
           };
  }

  private object BuildArea(string name)
  {
    return new { name };
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

  private async Task<Guid> CreateAreaAsync(string name)
  {
    using var response = await _context.Client.PostAsJsonAsync($"{FestivalPath}/areas", BuildArea(name));

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

  private async Task<IReadOnlyList<string?>> AreaNamesInOrderAsync()
  {
    using var response = await _context.Client.GetAsync($"{FestivalPath}/table-plan");

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("areas").EnumerateArray().Select(area => area.GetProperty("name").GetString()).ToList();
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

    using var response = await _context.Client.PostAsJsonAsync($"/api/admin/festivals/{otherFestivalId}/areas", BuildArea("Garten"));

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

  private async Task<JsonElement> ListedAreaAsync(Guid areaId)
  {
    using var response = await _context.Client.GetAsync($"{FestivalPath}/table-plan");

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("areas").EnumerateArray().Single(area => area.GetProperty("areaId").GetGuid() == areaId);
  }

  private async Task MoveTheFestivalAsync(int startsInHours, int endsInHours)
  {
    await using var database = _context.Factory.CreateContext();
    var festival = await database.Festivals.SingleAsync(candidate => candidate.Id == _context.World.FestivalId);
    festival.StartsAtUtc = DateTime.UtcNow.AddHours(startsInHours);
    festival.EndsAtUtc = DateTime.UtcNow.AddHours(endsInHours);
    await database.SaveChangesAsync();
  }

  private async Task LetTheFestivalEndAsync()
  {
    await using var database = _context.Factory.CreateContext();
    await database.Festivals.Where(festival => festival.Id == _context.World.FestivalId).ExecuteUpdateAsync(festival => festival.SetProperty(entry => entry.EndsAtUtc, DateTime.UtcNow.AddMinutes(-1)));
  }
}
