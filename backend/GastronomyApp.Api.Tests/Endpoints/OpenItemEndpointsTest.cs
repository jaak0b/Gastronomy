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
    _context = await new OrderTestContext.Builder().StartAsync();
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
    await PlaceOrderAsync("Tisch 12", settleOnSend: false);

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
    await PlaceOrderAsync("Tisch 12", settleOnSend: true);

    var body = await ReadOpenItemsAsync();

    Assert.That(body.RootElement.GetProperty("tables").GetArrayLength(), Is.Zero);
  }

  [Test]
  public async Task GetTableNames_AnOrderSentAndSettled_StillOffersTheNameForTheNextOrder()
  {
    await PlaceOrderAsync("Tisch 12", settleOnSend: true);

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
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12", settleOnSend: false);

    using var given = await _context.SendAsync(HttpMethod.Post,
                                               "/api/open-items/settle",
                                               new SettleItemsBody(itemIds, 0, "Essen fuer die Kapelle"));
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
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12", settleOnSend: false);

    using var settled = await _context.SendAsync(HttpMethod.Post,
                                                 "/api/open-items/settle",
                                                 new SettleItemsBody(itemIds, 700));
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
    await PlaceOrderAsync("Tisch 12", settleOnSend: false);

    var body = await ReadOpenItemsAsync();

    Assert.That(body.RootElement.GetProperty("itemsWithoutAnOrderCount").GetInt32(), Is.Zero);
  }

  [Test]
  public async Task PostOrder_SentAndSettled_ChargesTheDisplayedPriceOnEveryItem()
  {
    await PlaceOrderAsync("Tisch 12", settleOnSend: true);

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
    await PlaceOrderAsync("Tisch 12", settleOnSend: true);

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.That(stored.Select(item => item.SettledByStaffMemberId),
                Is.All.EqualTo(_context.World.StaffMemberId));
  }

  [Test]
  public async Task PostOrder_SentWithoutSettling_LeavesTheCollectingWaiterUnwritten()
  {
    await PlaceOrderAsync("Tisch 12", settleOnSend: false);

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.That(stored.Select(item => item.SettledByStaffMemberId), Is.All.Null);
  }

  [Test]
  public async Task PostSettle_TheItemsOfATable_RecordsTheWaiterWhoCollectedTheMoney()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12", settleOnSend: false);

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody(itemIds, 700));

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
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12", settleOnSend: false);

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody(itemIds, 0, "Essen fuer die Kapelle"));

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
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12", settleOnSend: false);

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody(itemIds, 700));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var open = await ReadOpenItemsAsync();
    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("settledOrderItemIds").GetArrayLength(), Is.EqualTo(2));
                      Assert.That(body.RootElement.GetProperty("alreadySettledOrderItemIds").GetArrayLength(), Is.Zero);
                      Assert.That(body.RootElement.GetProperty("otherPhonesWereTold").GetBoolean(), Is.True);
                      Assert.That(open.RootElement.GetProperty("tables").GetArrayLength(), Is.Zero);
                      Assert.That(stored.Select(item => item.ChargedPriceCents), Is.All.EqualTo(350));
                      Assert.That(stored.Select(item => item.PaymentNotice), Is.All.Null);
                    });
  }

  [Test]
  public async Task PostSettle_APartOfWhatTheTableOwes_SplitsTheAmountAcrossTheItemsAndClearsThem()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12", settleOnSend: false);

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody(itemIds, 300, "Der Tisch zahlt den Rest spaeter"));
    var open = await ReadOpenItemsAsync();
    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.Select(item => item.ChargedPriceCents), Is.All.EqualTo(150));
                      Assert.That(stored.Sum(item => item.ChargedPriceCents), Is.EqualTo(300));
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
  public async Task PostSettle_WithoutAnAmount_IsRefusedWithWordingThePhoneCanShow()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12", settleOnSend: false);

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody(itemIds, null));
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
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12", settleOnSend: false);

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody(itemIds, -100));
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
  public async Task PostSettle_TheSameSelectionASecondTime_SettlesNothingFurther()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12", settleOnSend: false);

    using var first = await _context.SendAsync(HttpMethod.Post,
                                               "/api/open-items/settle",
                                               new SettleItemsBody(itemIds, 700));
    using var second = await _context.SendAsync(HttpMethod.Post,
                                                "/api/open-items/settle",
                                                new SettleItemsBody(itemIds, 700));
    var body = JsonDocument.Parse(await second.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("settledOrderItemIds").GetArrayLength(), Is.Zero);
                      Assert.That(body.RootElement.GetProperty("alreadySettledOrderItemIds").GetArrayLength(),
                                  Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task PostSettle_NothingAtAllWithAReason_ChargesNothingAndKeepsTheDisplayedPrice()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12", settleOnSend: false);

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody(itemIds, 0, "Essen fuer die Kapelle"));

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
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12", settleOnSend: false);

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody(itemIds, 300, "   "));
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
  public async Task PostSettle_AnItemThatWasAlreadyGivenAway_KeepsTheReasonThatWasTypedFirst()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12", settleOnSend: false);

    using var first = await _context.SendAsync(HttpMethod.Post,
                                               "/api/open-items/settle",
                                               new SettleItemsBody(itemIds, 0, "Kapelle"));
    using var second = await _context.SendAsync(HttpMethod.Post,
                                                "/api/open-items/settle",
                                                new SettleItemsBody(itemIds, 0, "Versehen"));

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.Select(item => item.PaymentNotice), Is.All.EqualTo("Kapelle"));
                    });
  }

  [Test]
  public async Task PostSettle_ASelectionHoldingAnItemTheLaptopDoesNotKnow_SettlesNothingAtAll()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync("Tisch 12", settleOnSend: false);

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody([.. itemIds, Guid.NewGuid()], 700));
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
    IReadOnlyList<Guid> twelve = await PlaceOrderAsync("Tisch 12", settleOnSend: false);
    IReadOnlyList<Guid> three = await PlaceOrderAsync("Tisch 3", settleOnSend: false);

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody([.. twelve, .. three], 1400));
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
    await PlaceOrderAsync("Tisch 12", settleOnSend: false);
    await PlaceOrderAsync("Tisch 3", settleOnSend: false);

    var body = await ReadOpenItemsAsync();

    Assert.That(body.RootElement.GetProperty("tables")
                    .EnumerateArray()
                    .Select(table => table.GetProperty("tableName").GetString()),
                Is.EqualTo(new[] { "Tisch 12", "Tisch 3" }));
  }

  [Test]
  public async Task GetOpenItems_AnItemWhoseOrderCannotBeFound_SaysHowManyItemsTheListIsMissing()
  {
    await PlaceOrderAsync("Tisch 12", settleOnSend: false);
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

  private async Task<IReadOnlyList<Guid>> PlaceOrderAsync(string tableName, bool settleOnSend)
  {
    OrderBody order = new(Guid.NewGuid(),
                          tableName,
                          null,
                          [
                            new(_context.World.BratwurstItemId, 350, null, null),
                            new(_context.World.BratwurstItemId, 350, null, null)
                          ],
                          settleOnSend);

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
