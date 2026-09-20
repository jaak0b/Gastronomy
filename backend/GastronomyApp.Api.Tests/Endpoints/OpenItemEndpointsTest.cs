using System.Net;
using System.Text.Json;
using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class OpenItemEndpointsTest
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
  public async Task GetOpenItems_AnOrderSentWithoutSettling_ListsTheTableWithWhatItStillOwes()
  {
    await PlaceOrderAsync("Tisch 12");

    var body = await ReadOpenItemsAsync();
    var tables = body.RootElement.GetProperty("tables");

    Assert.Multiple(() =>
                    {
                      Assert.That(tables.GetArrayLength(), Is.EqualTo(1));
                      Assert.That(tables[0].GetProperty("tableName").GetString(), Is.EqualTo("Tisch 12"));
                      Assert.That(tables[0].GetProperty("openAmountCents").GetInt32(), Is.EqualTo(700));
                      Assert.That(tables[0].GetProperty("items").GetArrayLength(), Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task GetOpenItems_AnOrderSentAndSettled_LeavesTheTableOut()
  {
    await PlaceOrderAsync("Tisch 12", settled: true);

    var body = await ReadOpenItemsAsync();

    Assert.That(body.RootElement.GetProperty("tables").GetArrayLength(), Is.Zero);
  }

  [Test]
  public async Task GetTableNames_AnOrderSentAndSettled_StillOffersTheNameForTheNextOrder()
  {
    await PlaceOrderAsync("Tisch 12", settled: true);

    using var response = await _context.SendAsync(HttpMethod.Get, "/api/open-items/table-names");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.That(body.RootElement.GetProperty("tableNames")
                    .EnumerateArray()
                    .Select(name => name.GetString()),
                Is.EqualTo(new[] { "Tisch 12" }));
  }

  [Test]
  public async Task GetOpenItems_ATableWhereEverythingWasGivenAway_StillNamesTheTableWithTheReason()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12");

    using var given = await _context.SendAsync(HttpMethod.Post,
                                               "/api/open-items/settle",
                                               SettleBodyFor(itemIds, 0, "Essen fuer die Kapelle"));
    var body = await ReadOpenItemsAsync();
    var table = body.RootElement.GetProperty("tables")[0];

    Assert.Multiple(() =>
                    {
                      Assert.That(given.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(table.GetProperty("tableName").GetString(), Is.EqualTo("Tisch 12"));
                      Assert.That(table.GetProperty("openAmountCents").GetInt32(), Is.Zero);
                      Assert.That(table.GetProperty("items").GetArrayLength(), Is.Zero);
                      Assert.That(table.GetProperty("givenAwayAmountCents").GetInt32(), Is.EqualTo(700));
                      Assert.That(table.GetProperty("givenAwayItems").GetArrayLength(), Is.EqualTo(2));
                      Assert.That(table.GetProperty("givenAwayItems")[0].GetProperty("paymentNotice").GetString(),
                                  Is.EqualTo("Essen fuer die Kapelle"));
                      Assert.That(table.GetProperty("givenAwayItems")[0].GetProperty("waivedAmountCents").GetInt32(),
                                  Is.EqualTo(350));
                    });
  }

  [Test]
  public async Task GetOpenItems_ATableThatPaidTheFullAmount_ShowsNothingAsGivenAway()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12");

    using var settled = await _context.SendAsync(HttpMethod.Post,
                                                 "/api/open-items/settle",
                                                 SettleBodyFor(itemIds, 350));
    var body = await ReadOpenItemsAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(settled.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("tables").GetArrayLength(), Is.Zero);
                    });
  }

  [Test]
  public async Task GetOpenItems_NothingBroken_ReportsThatNoItemIsMissingFromTheList()
  {
    await PlaceOrderAsync("Tisch 12");

    var body = await ReadOpenItemsAsync();

    Assert.That(body.RootElement.GetProperty("itemsWithoutAnOrderCount").GetInt32(), Is.Zero);
  }

  [Test]
  public async Task PostOrder_SentAndSettled_ChargesTheDisplayedPriceOnEveryItem()
  {
    await PlaceOrderAsync("Tisch 12", settled: true);

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(stored.Select(item => item.ChargedPriceCents), Is.All.EqualTo(350));
                      Assert.That(stored.Select(item => item.UnitPriceCents), Is.All.EqualTo(350));
                      Assert.That(stored.Select(item => item.SettledAtUtc), Is.All.Not.Null);
                      Assert.That(stored.Select(item => item.PaymentNotice), Is.All.Null);
                    });
  }

  [Test]
  public async Task PostOrder_SentAndSettled_RecordsTheSendingWaiterAsTheOneWhoCollectedTheMoney()
  {
    await PlaceOrderAsync("Tisch 12", settled: true);

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.That(stored.Select(item => item.SettledByStaffMemberId),
                Is.All.EqualTo(_context.World.StaffMemberId));
  }

  [Test]
  public async Task PostOrder_SentWithoutSettling_LeavesTheCollectingWaiterUnwritten()
  {
    await PlaceOrderAsync("Tisch 12");

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.That(stored.Select(item => item.SettledByStaffMemberId), Is.All.Null);
  }

  [Test]
  public async Task PostSettle_TheItemsOfATable_RecordsTheWaiterWhoCollectedTheMoney()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12");

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  SettleBodyFor(itemIds, 350));

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.Select(item => item.SettledByStaffMemberId),
                                  Is.All.EqualTo(_context.World.StaffMemberId));
                    });
  }

  [Test]
  public async Task PostSettle_NothingAtAllWithAReason_RecordsTheWaiterWhoGaveTheItemsAway()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12");

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  SettleBodyFor(itemIds, 0, "Essen fuer die Kapelle"));

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.Select(item => item.SettledByStaffMemberId),
                                  Is.All.EqualTo(_context.World.StaffMemberId));
                    });
  }

  [Test]
  public async Task PostSettle_TheFullAmount_ChargesEveryItemItsOwnPriceAndClearsTheTable()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12");

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  SettleBodyFor(itemIds, 350));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var open = await ReadOpenItemsAsync();
    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("settledOrderItemIds").GetArrayLength(), Is.EqualTo(2));
                      Assert.That(body.RootElement.GetProperty("reappliedOrderItemIds").GetArrayLength(), Is.Zero);
                      Assert.That(body.RootElement.GetProperty("alreadySettledByOthersOrderItemIds").GetArrayLength(),
                                  Is.Zero);
                      Assert.That(body.RootElement.GetProperty("otherPhonesWereTold").GetBoolean(), Is.True);
                      Assert.That(open.RootElement.GetProperty("tables").GetArrayLength(), Is.Zero);
                      Assert.That(stored.Select(item => item.ChargedPriceCents), Is.All.EqualTo(350));
                      Assert.That(stored.Select(item => item.PaymentNotice), Is.All.Null);
                    });
  }

  [Test]
  public async Task PostSettle_OnePricePerLine_StoresExactlyThePricesThePhoneSent()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12");

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody([
                                                                        new(itemIds[0], 100, "Der Tisch zahlt den Rest spaeter"),
                                                                        new(itemIds[1], 200, "Der Tisch zahlt den Rest spaeter")
                                                                      ]));
    var open = await ReadOpenItemsAsync();
    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.Single(item => item.Id == itemIds[0]).ChargedPriceCents,
                                  Is.EqualTo(100));
                      Assert.That(stored.Single(item => item.Id == itemIds[1]).ChargedPriceCents,
                                  Is.EqualTo(200));
                      Assert.That(stored.Select(item => item.UnitPriceCents), Is.All.EqualTo(350));
                      Assert.That(open.RootElement.GetProperty("tables")[0].GetProperty("openAmountCents").GetInt32(),
                                  Is.Zero);
                      Assert.That(open.RootElement.GetProperty("tables")[0]
                                      .GetProperty("givenAwayAmountCents")
                                      .GetInt32(),
                                  Is.EqualTo(400));
                    });
  }

  [Test]
  public async Task PostSettle_APriceAboveTheDisplayedPrice_IsStoredAsSent()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12");

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody([new(itemIds[0], 500), new(itemIds[1], 350)]));

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.Single(item => item.Id == itemIds[0]).ChargedPriceCents,
                                  Is.EqualTo(500));
                      Assert.That(stored.Single(item => item.Id == itemIds[1]).ChargedPriceCents,
                                  Is.EqualTo(350));
                    });
  }

  [Test]
  public async Task PostSettle_WithoutAnAmount_IsRefusedWithWordingThePhoneCanShow()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12");

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  SettleBodyFor(itemIds, null));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var open = await ReadOpenItemsAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("order.settlementCannotBeProcessed"));
                      Assert.That(open.RootElement.GetProperty("tables")[0].GetProperty("openAmountCents").GetInt32(),
                                  Is.EqualTo(700));
                    });
  }

  [Test]
  public async Task PostSettle_ANegativeAmount_IsRefusedWithWordingThePhoneCanShow()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12");

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  SettleBodyFor(itemIds, -100));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var open = await ReadOpenItemsAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("order.settlementCannotBeProcessed"));
                      Assert.That(open.RootElement.GetProperty("tables")[0].GetProperty("openAmountCents").GetInt32(),
                                  Is.EqualTo(700));
                    });
  }

  [Test]
  public async Task PostSettle_ASelectionNamingOneItemTwice_SettlesNothingAtAll()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12");

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody([new(itemIds[0], 350), new(itemIds[0], 350)]));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var open = await ReadOpenItemsAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("order.settlementCannotBeProcessed"));
                      Assert.That(open.RootElement.GetProperty("tables")[0].GetProperty("openAmountCents").GetInt32(),
                                  Is.EqualTo(700));
                    });
  }

  [Test]
  public async Task PostSettle_NothingSelected_IsRefusedWithWordingThePhoneCanShow()
  {
    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody([]));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("order.settlementNoItemsSelected"));
                    });
  }

  [Test]
  public async Task PostSettle_TheSameSelectionASecondTime_SettlesNothingFurther()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12");

    using var first = await _context.SendAsync(HttpMethod.Post,
                                               "/api/open-items/settle",
                                               SettleBodyFor(itemIds, 350));
    using var second = await _context.SendAsync(HttpMethod.Post,
                                                "/api/open-items/settle",
                                                SettleBodyFor(itemIds, 350));
    var body = JsonDocument.Parse(await second.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("settledOrderItemIds").GetArrayLength(), Is.Zero);
                      Assert.That(body.RootElement.GetProperty("reappliedOrderItemIds").GetArrayLength(),
                                  Is.EqualTo(2));
                      Assert.That(body.RootElement.GetProperty("alreadySettledByOthersOrderItemIds").GetArrayLength(),
                                  Is.Zero);
                    });
  }

  [Test]
  public async Task PostSettle_NothingAtAllWithAReason_ChargesNothingAndKeepsTheDisplayedPrice()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12");

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  SettleBodyFor(itemIds, 0, "Essen fuer die Kapelle"));

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.Select(item => item.ChargedPriceCents), Is.All.Zero);
                      Assert.That(stored.Select(item => item.UnitPriceCents), Is.All.EqualTo(350));
                      Assert.That(stored.Select(item => item.PaymentNotice), Is.All.EqualTo("Essen fuer die Kapelle"));
                    });
  }

  [Test]
  public async Task PostSettle_LessThanTheTableOwesWithoutAReason_IsRefusedWithWordingThePhoneCanShow()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12");

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  SettleBodyFor(itemIds, 300, "   "));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var open = await ReadOpenItemsAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("order.settlementCannotBeProcessed"));
                      Assert.That(open.RootElement.GetProperty("tables")[0].GetProperty("openAmountCents").GetInt32(),
                                  Is.EqualTo(700));
                    });
  }

  [Test]
  public async Task PostSettle_AnItemTheSameWaiterSettlesAgain_TakesTheNewPriceAndReason()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12");

    using var first = await _context.SendAsync(HttpMethod.Post,
                                               "/api/open-items/settle",
                                               SettleBodyFor(itemIds, 0, "Kapelle"));
    using var second = await _context.SendAsync(HttpMethod.Post,
                                                "/api/open-items/settle",
                                                SettleBodyFor(itemIds, 100, "Versehen"));

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.Select(item => item.ChargedPriceCents), Is.All.EqualTo(100));
                      Assert.That(stored.Select(item => item.PaymentNotice), Is.All.EqualTo("Versehen"));
                    });
  }

  [Test]
  public async Task PostSettle_ALineAColleagueAlreadySettled_IsReportedBackAndLeftUntouched()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12");
    string colleagueToken = await _context.IssueSecondStaffTokenAsync("Bernd");

    using var colleagueSettlement = await _context.SendAsAsync(colleagueToken,
                                                               HttpMethod.Post,
                                                               "/api/open-items/settle",
                                                               new SettleItemsBody([new(itemIds[0], 0, "Kapelle")]));
    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  SettleBodyFor(itemIds, 350));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();
    var colleagueLine = stored.Single(item => item.Id == itemIds[0]);
    var ownLine = stored.Single(item => item.Id == itemIds[1]);

    Assert.Multiple(() =>
                    {
                      Assert.That(colleagueSettlement.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("settledOrderItemIds").GetArrayLength(), Is.EqualTo(1));
                      Assert.That(body.RootElement.GetProperty("alreadySettledByOthersOrderItemIds").GetArrayLength(),
                                  Is.EqualTo(1));
                      Assert.That(colleagueLine.ChargedPriceCents, Is.Zero);
                      Assert.That(colleagueLine.PaymentNotice, Is.EqualTo("Kapelle"));
                      Assert.That(colleagueLine.SettledByStaffMemberId, Is.Not.EqualTo(_context.World.StaffMemberId));
                      Assert.That(ownLine.ChargedPriceCents, Is.EqualTo(350));
                    });
  }

  [Test]
  public async Task PostSettle_ASelectionHoldingAnItemTheLaptopDoesNotKnow_SettlesNothingAtAll()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12");

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody([
                                                                        new(itemIds[0], 350),
                                                                        new(itemIds[1], 350),
                                                                        new(Guid.NewGuid(), 350)
                                                                      ]));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var open = await ReadOpenItemsAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("order.settlementUnknownItem"));
                      Assert.That(open.RootElement.GetProperty("tables")[0].GetProperty("openAmountCents").GetInt32(),
                                  Is.EqualTo(700));
                    });
  }

  [Test]
  public async Task PostSettle_ASelectionSpanningTwoTables_SettlesNothingAtAll()
  {
    IReadOnlyList<Guid> twelve = await PlaceOrderAsync("Tisch 12");
    IReadOnlyList<Guid> three = await PlaceOrderAsync("Tisch 3");

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody([
                                                                        new(twelve[0], 350),
                                                                        new(twelve[1], 350),
                                                                        new(three[0], 350),
                                                                        new(three[1], 350)
                                                                      ]));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var open = await ReadOpenItemsAsync();
    var tables = open.RootElement.GetProperty("tables");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("order.settlementCannotBeProcessed"));
                      Assert.That(tables.GetArrayLength(), Is.EqualTo(2));
                      Assert.That(tables[0].GetProperty("openAmountCents").GetInt32(), Is.EqualTo(700));
                      Assert.That(tables[1].GetProperty("openAmountCents").GetInt32(), Is.EqualTo(700));
                    });
  }

  [Test]
  public async Task GetOpenItems_TwoTablesWithSomethingOpen_KeepsEachTableApart()
  {
    await PlaceOrderAsync("Tisch 12");
    await PlaceOrderAsync("Tisch 3");

    var body = await ReadOpenItemsAsync();

    Assert.That(body.RootElement.GetProperty("tables")
                    .EnumerateArray()
                    .Select(table => table.GetProperty("tableName").GetString()),
                Is.EqualTo(new[] { "Tisch 12", "Tisch 3" }));
  }

  [Test]
  public async Task GetOpenItems_AnItemWhoseOrderCannotBeFound_SaysHowManyItemsTheListIsMissing()
  {
    await PlaceOrderAsync("Tisch 12");
    await AddAnItemWithoutAnOrderAsync();

    var body = await ReadOpenItemsAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(body.RootElement.GetProperty("itemsWithoutAnOrderCount").GetInt32(),
                                  Is.EqualTo(1));
                      Assert.That(body.RootElement.GetProperty("tables")[0]
                                      .GetProperty("openAmountCents")
                                      .GetInt32(),
                                  Is.EqualTo(700));
                    });
  }

  [Test]
  public async Task GetOpenItems_WithoutADeviceToken_IsRefused()
  {
    using HttpRequestMessage request = new(HttpMethod.Get, "/api/open-items");
    using var response = await _context.Client.SendAsync(request);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  private SettleItemsBody SettleBodyFor(IReadOnlyList<Guid> orderItemIds,
                                        int? paidPriceCents,
                                        string? paymentNotice = null)
  {
    return new([.. orderItemIds.Select(orderItemId => new SettleLineBody(orderItemId, paidPriceCents, paymentNotice))]);
  }

  private async Task AddAnItemWithoutAnOrderAsync()
  {
    await using var database = _context.Factory.CreateContext();
    await database.Database.OpenConnectionAsync();
    await database.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");

    database.OrderItems.Add(new()
                            {
                              Id = Guid.NewGuid(),
                              StationOrderId = Guid.NewGuid(),
                              CatalogItemId = _context.World.BratwurstItemId,
                              ItemName = "Bratwurst mit Brot",
                              UnitPriceCents = 350
                            });

    await database.SaveChangesAsync();
    await database.Database.CloseConnectionAsync();
  }

  private async Task<JsonDocument> ReadOpenItemsAsync()
  {
    using var response = await _context.SendAsync(HttpMethod.Get, "/api/open-items");

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
  }

  private async Task<IReadOnlyList<Guid>> PlaceOrderAsync(string tableName, bool settled = false)
  {
    OrderItemSettlementBody? settlement = settled ? new(350) : null;
    OrderBody order = new(Guid.NewGuid(),
                          tableName,
                          null,
                          [
                            new(_context.World.BratwurstItemId, 350, null, null, settlement),
                            new(_context.World.BratwurstItemId, 350, null, null, settlement)
                          ]);

    using var response = await _context.PostOrderAsync(order);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return
    [
      .. body.RootElement
             .GetProperty("stationOrders")
             .EnumerateArray()
             .SelectMany(stationOrder => stationOrder.GetProperty("itemIds").EnumerateArray())
             .Select(itemId => itemId.GetGuid())
    ];
  }
}
