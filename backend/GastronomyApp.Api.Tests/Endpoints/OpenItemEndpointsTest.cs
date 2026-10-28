using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.Tests.TestSupport;
using GastronomyApp.Contracts.Enums;
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
  public async Task SearchTable_ASeatedTable_ListsEveryOrderAndPositionWithItsState()
  {
    await PlaceOrderAsync(_context.World.TableId);
    IReadOnlyList<Guid> newestItems = await PlaceOrderAsync(_context.World.TableId);
    await FulfillAndSettleAsync(newestItems[0]);

    using var response = await _context.SendAsync(HttpMethod.Post, $"/api/open-items/tables/{_context.World.TableId}/search", new TableSearchBody(null, null, null));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var orders = body.RootElement.GetProperty("orders");
    var newestOrder = orders[0];
    var settledItem = newestOrder.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("orderItemId").GetGuid() == newestItems[0]);
    var openItem = newestOrder.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("orderItemId").GetGuid() == newestItems[1]);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("tableId").GetGuid(), Is.EqualTo(_context.World.TableId));
                      Assert.That(body.RootElement.GetProperty("tableName").GetString(), Is.EqualTo("Tisch 12"));
                      Assert.That(orders.GetArrayLength(), Is.EqualTo(2));
                      Assert.That(newestOrder.GetProperty("globalOrderNumber").GetInt32(), Is.EqualTo(2));
                      Assert.That(orders[1].GetProperty("globalOrderNumber").GetInt32(), Is.EqualTo(1));
                      Assert.That(newestOrder.GetProperty("staffMemberName").GetString(), Is.EqualTo("Anna"));
                      Assert.That(newestOrder.GetProperty("createdAtUtc").GetString(), Is.Not.Empty);
                      Assert.That(newestOrder.GetProperty("items").GetArrayLength(), Is.EqualTo(2));
                      Assert.That(settledItem.GetProperty("orderId").GetGuid(), Is.EqualTo(newestOrder.GetProperty("orderId").GetGuid()));
                      Assert.That(settledItem.GetProperty("globalOrderNumber").GetInt32(), Is.EqualTo(2));
                      Assert.That(settledItem.GetProperty("itemName").GetString(), Is.EqualTo("Bratwurst mit Brot"));
                      Assert.That(settledItem.GetProperty("note").ValueKind, Is.EqualTo(JsonValueKind.Null));
                      Assert.That(settledItem.GetProperty("unitPriceCents").GetInt32(), Is.EqualTo(350));
                      Assert.That(settledItem.GetProperty("state").GetString(), Is.EqualTo("handedOut"));
                      Assert.That(settledItem.GetProperty("stateSetAtUtc").ValueKind, Is.EqualTo(JsonValueKind.String));
                      Assert.That(settledItem.GetProperty("settledAtUtc").ValueKind, Is.EqualTo(JsonValueKind.String));
                      Assert.That(settledItem.GetProperty("chargedPriceCents").GetInt32(), Is.EqualTo(350));
                      Assert.That(openItem.GetProperty("state").GetString(), Is.EqualTo("open"));
                      Assert.That(openItem.GetProperty("stateSetAtUtc").ValueKind, Is.EqualTo(JsonValueKind.String));
                      Assert.That(openItem.GetProperty("settledAtUtc").ValueKind, Is.EqualTo(JsonValueKind.Null));
                      Assert.That(openItem.GetProperty("chargedPriceCents").ValueKind, Is.EqualTo(JsonValueKind.Null));
                    });
  }

  [Test]
  public async Task SearchTable_ATableNobodyHasOrderedAt_AnswersAnEmptyReportUnderItsName()
  {
    await PlaceOrderAsync(_context.World.TableId);
    var emptyTableId = await _context.SeedTableAsync("Tisch 99");

    using var response = await _context.SendAsync(HttpMethod.Post, $"/api/open-items/tables/{emptyTableId}/search", new TableSearchBody(null, null, null));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("tableId").GetGuid(), Is.EqualTo(emptyTableId));
                      Assert.That(body.RootElement.GetProperty("tableName").GetString(), Is.EqualTo("Tisch 99"));
                      Assert.That(body.RootElement.GetProperty("orders").GetArrayLength(), Is.Zero);
                    });
  }

  [Test]
  public async Task SearchTable_ATableTheRunningFestivalDoesNotHold_IsNotFound()
  {
    await PlaceOrderAsync(_context.World.TableId);

    using var response = await _context.SendAsync(HttpMethod.Post, $"/api/open-items/tables/{Guid.NewGuid()}/search", new TableSearchBody(null, null, null));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
  }

  [Test]
  public async Task SearchTable_WithoutADeviceToken_IsRefused()
  {
    using HttpRequestMessage request = new(HttpMethod.Post, $"/api/open-items/tables/{_context.World.TableId}/search");
    request.Content = JsonContent.Create(new TableSearchBody(null, null, null));
    using var response = await _context.Client.SendAsync(request);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  [Test]
  public async Task SearchTable_ATableRenamedAfterItsOrder_AnswersUnderTheCurrentName()
  {
    await PlaceOrderAsync(_context.World.TableId);
    await RenameTheTableAsync(_context.World.TableId, "Stammtisch");

    var body = await SearchTableAsync(_context.World.TableId);
    var table = body.RootElement;

    Assert.Multiple(() =>
                    {
                      Assert.That(table.GetProperty("tableId").GetGuid(), Is.EqualTo(_context.World.TableId));
                      Assert.That(table.GetProperty("tableName").GetString(), Is.EqualTo("Stammtisch"));
                    });
  }

  [Test]
  public async Task PostOrder_SentWithoutSettling_LeavesTheCollectingWaiterUnwritten()
  {
    await PlaceOrderAsync(_context.World.TableId);

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.That(stored.Select(item => item.SettledByStaffMemberId), Is.All.Null);
  }

  [Test]
  public async Task PostSettle_TheItemsOfATable_RecordsTheWaiterWhoCollectedTheMoney()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", SettleBodyFor(itemIds, 350));

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.Select(item => item.SettledByStaffMemberId), Is.All.EqualTo(_context.World.StaffMemberId));
                    });
  }

  [Test]
  public async Task PostSettle_NothingAtAllWithAReason_RecordsTheWaiterWhoGaveTheItemsAway()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", SettleBodyFor(itemIds, 0, "Essen fuer die Kapelle"));

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.Select(item => item.SettledByStaffMemberId), Is.All.EqualTo(_context.World.StaffMemberId));
                    });
  }

  [Test]
  public async Task PostSettle_TheFullAmount_ChargesEveryItemItsOwnPriceAndClearsTheTable()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", SettleBodyFor(itemIds, 350));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var open = await SearchTableAsync(_context.World.TableId);
    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("settledOrderItemIds").GetArrayLength(), Is.EqualTo(2));
                      Assert.That(body.RootElement.GetProperty("reappliedOrderItemIds").GetArrayLength(), Is.Zero);
                      Assert.That(body.RootElement.GetProperty("alreadySettledByOthersOrderItemIds").GetArrayLength(), Is.Zero);
                      Assert.That(SettledAtUtcKindsIn(open), Is.All.EqualTo(JsonValueKind.String));
                      Assert.That(stored.Select(item => item.ChargedPriceCents), Is.All.EqualTo(350));
                      Assert.That(stored.Select(item => item.PaymentNotice), Is.All.Null);
                    });
  }

  [Test]
  public async Task PostSettle_OnePricePerLine_StoresExactlyThePricesThePhoneSent()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody([
                                                                        new(itemIds[0], 100, "Der Tisch zahlt den Rest spaeter"),
                                                                        new(itemIds[1], 200, "Der Tisch zahlt den Rest spaeter")
                                                                      ],
                                                                      "cash"));
    var open = await SearchTableAsync(_context.World.TableId);
    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.Single(item => item.Id == itemIds[0]).ChargedPriceCents, Is.EqualTo(100));
                      Assert.That(stored.Single(item => item.Id == itemIds[1]).ChargedPriceCents, Is.EqualTo(200));
                      Assert.That(stored.Select(item => item.UnitPriceCents), Is.All.EqualTo(350));
                      Assert.That(SettledAtUtcKindsIn(open), Is.All.EqualTo(JsonValueKind.String));
                    });
  }

  [Test]
  public async Task PostSettle_APriceAboveTheDisplayedPrice_IsStoredAsSent()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody([
                                                                        new(itemIds[0], 500),
                                                                        new(itemIds[1], 350)
                                                                      ],
                                                                      "cash"));

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.Single(item => item.Id == itemIds[0]).ChargedPriceCents, Is.EqualTo(500));
                      Assert.That(stored.Single(item => item.Id == itemIds[1]).ChargedPriceCents, Is.EqualTo(350));
                    });
  }

  [Test]
  public async Task PostSettle_WithoutAnAmount_IsRefusedWithWordingThePhoneCanShow()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", SettleBodyFor(itemIds, null));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var open = await SearchTableAsync(_context.World.TableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.settlement.cannotBeProcessed"));
                      Assert.That(SettledAtUtcKindsIn(open), Is.All.EqualTo(JsonValueKind.Null));
                    });
  }

  [Test]
  public async Task PostSettle_ANegativeAmount_IsRefusedWithWordingThePhoneCanShow()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", SettleBodyFor(itemIds, -100));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var open = await SearchTableAsync(_context.World.TableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.settlement.cannotBeProcessed"));
                      Assert.That(SettledAtUtcKindsIn(open), Is.All.EqualTo(JsonValueKind.Null));
                    });
  }

  [Test]
  public async Task PostSettle_ASelectionNamingOneItemTwice_SettlesNothingAtAll()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody([
                                                                        new(itemIds[0], 350),
                                                                        new(itemIds[0], 350)
                                                                      ],
                                                                      "cash"));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var open = await SearchTableAsync(_context.World.TableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.settlement.cannotBeProcessed"));
                      Assert.That(SettledAtUtcKindsIn(open), Is.All.EqualTo(JsonValueKind.Null));
                    });
  }

  [Test]
  public async Task PostSettle_NothingSelected_IsRefusedWithWordingThePhoneCanShow()
  {
    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", new SettleItemsBody([], "none"));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.settlement.noItemsSelected"));
                    });
  }

  [Test]
  public async Task PostSettle_TheSameSelectionASecondTime_SettlesNothingFurther()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var first = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", SettleBodyFor(itemIds, 350));
    using var second = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", SettleBodyFor(itemIds, 350));
    var body = JsonDocument.Parse(await second.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("settledOrderItemIds").GetArrayLength(), Is.Zero);
                      Assert.That(body.RootElement.GetProperty("reappliedOrderItemIds").GetArrayLength(), Is.EqualTo(2));
                      Assert.That(body.RootElement.GetProperty("alreadySettledByOthersOrderItemIds").GetArrayLength(), Is.Zero);
                    });
  }

  [Test]
  public async Task PostSettle_NothingAtAllWithAReason_ChargesNothingAndKeepsTheDisplayedPrice()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", SettleBodyFor(itemIds, 0, "Essen fuer die Kapelle"));

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
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", SettleBodyFor(itemIds, 300, "   "));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var open = await SearchTableAsync(_context.World.TableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.settlement.cannotBeProcessed"));
                      Assert.That(SettledAtUtcKindsIn(open), Is.All.EqualTo(JsonValueKind.Null));
                    });
  }

  [Test]
  public async Task PostSettle_AnItemTheSameWaiterSettlesAgain_TakesTheNewPriceAndReason()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var first = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", SettleBodyFor(itemIds, 0, "Kapelle"));
    using var second = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", SettleBodyFor(itemIds, 100, "Versehen"));

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
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);
    var colleagueToken = await _context.IssueSecondStaffTokenAsync("Bernd");

    using var colleagueSettlement = await _context.SendAsAsync(colleagueToken, HttpMethod.Post, "/api/open-items/settle", new SettleItemsBody([new(itemIds[0], 0, "Kapelle")], "none"));
    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", SettleBodyFor(itemIds, 350));
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
                      Assert.That(body.RootElement.GetProperty("alreadySettledByOthersOrderItemIds").GetArrayLength(), Is.EqualTo(1));
                      Assert.That(colleagueLine.ChargedPriceCents, Is.Zero);
                      Assert.That(colleagueLine.PaymentNotice, Is.EqualTo("Kapelle"));
                      Assert.That(colleagueLine.SettledByStaffMemberId, Is.Not.EqualTo(_context.World.StaffMemberId));
                      Assert.That(ownLine.ChargedPriceCents, Is.EqualTo(350));
                    });
  }

  [Test]
  public async Task PostSettle_TwoPhonesSettleTheSameLineAtTheSameMoment_SettlesItExactlyOnce()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);
    var colleagueToken = await _context.IssueSecondStaffTokenAsync("Bernd");

    Task<HttpResponseMessage> own = _context.SendAsAsync(_context.DeviceToken, HttpMethod.Post, "/api/open-items/settle", SettleBodyFor([itemIds[0]], 350));
    Task<HttpResponseMessage> colleague = _context.SendAsAsync(colleagueToken, HttpMethod.Post, "/api/open-items/settle", SettleBodyFor([itemIds[0]], 350));

    HttpResponseMessage[] responses = await Task.WhenAll(own, colleague);
    List<JsonDocument> bodies = [];

    foreach (var response in responses)
    {
      bodies.Add(JsonDocument.Parse(await response.Content.ReadAsStringAsync()));
      response.Dispose();
    }

    var settledCount = bodies.Sum(body => body.RootElement.GetProperty("settledOrderItemIds").GetArrayLength());
    var alreadySettledCount = bodies.Sum(body => body.RootElement.GetProperty("alreadySettledByOthersOrderItemIds").GetArrayLength());
    await using var database = _context.Factory.CreateContext();
    var storedLine = await database.OrderItems.SingleAsync(item => item.Id == itemIds[0]);

    Assert.Multiple(() =>
                    {
                      Assert.That(responses.Select(response => response.StatusCode), Is.All.EqualTo(HttpStatusCode.OK));
                      Assert.That(settledCount, Is.EqualTo(1));
                      Assert.That(alreadySettledCount, Is.EqualTo(1));
                      Assert.That(storedLine.SettledAtUtc, Is.Not.Null);
                      Assert.That(storedLine.ChargedPriceCents, Is.EqualTo(350));
                    });
  }

  [Test]
  public async Task PostSettle_ASelectionHoldingAnItemTheLaptopDoesNotKnow_SettlesNothingAtAll()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody([
                                                                        new(itemIds[0], 350),
                                                                        new(itemIds[1], 350),
                                                                        new(Guid.NewGuid(), 350)
                                                                      ],
                                                                      "cash"));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var open = await SearchTableAsync(_context.World.TableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.settlement.unknownItem"));
                      Assert.That(SettledAtUtcKindsIn(open), Is.All.EqualTo(JsonValueKind.Null));
                    });
  }

  [Test]
  public async Task PostSettle_ASelectionSpanningTwoTables_SettlesNothingAtAll()
  {
    var tableThreeId = await _context.SeedTableAsync("Tisch 3");
    IReadOnlyList<Guid> twelve = await PlaceOrderAsync(_context.World.TableId);
    IReadOnlyList<Guid> three = await PlaceOrderAsync(tableThreeId);

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody([
                                                                        new(twelve[0], 350),
                                                                        new(twelve[1], 350),
                                                                        new(three[0], 350),
                                                                        new(three[1], 350)
                                                                      ],
                                                                      "cash"));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var openAtTwelve = await SearchTableAsync(_context.World.TableId);
    var openAtThree = await SearchTableAsync(tableThreeId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.settlement.cannotBeProcessed"));
                      Assert.That(SettledAtUtcKindsIn(openAtTwelve), Is.All.EqualTo(JsonValueKind.Null));
                      Assert.That(SettledAtUtcKindsIn(openAtThree), Is.All.EqualTo(JsonValueKind.Null));
                    });
  }

  [Test]
  public async Task PostSettle_PaidInCash_StoresCashOnEveryPaidItem()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", SettleBodyFor(itemIds, 350));

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.Select(item => item.PaymentMethod), Is.All.EqualTo(PaymentMethod.Cash));
                    });
  }

  [Test]
  public async Task PostSettle_PaidByCardWithOneArticleGivenAway_StoresCardOnThePaidItemAndNoneOnTheFreeOne()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await _context.SendAsync(HttpMethod.Post,
                                                  "/api/open-items/settle",
                                                  new SettleItemsBody([
                                                                        new(itemIds[0], 350),
                                                                        new(itemIds[1], 0, "Kapelle")
                                                                      ],
                                                                      "card"));

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stored.Single(item => item.Id == itemIds[0]).PaymentMethod, Is.EqualTo(PaymentMethod.Card));
                      Assert.That(stored.Single(item => item.Id == itemIds[1]).PaymentMethod, Is.EqualTo(PaymentMethod.None));
                    });
  }

  [Test]
  public async Task PostSettle_WithoutAPaymentMethod_SettlesNothingAndAnswersTheSettlementWording()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", new { lines = itemIds.Select(itemId => new SettleLineBody(itemId, 350)).ToList() });

    await AssertSettlementRefusedAndNothingStoredAsync(response);
  }

  [TestCase("7")]
  [TestCase("null")]
  public async Task PostSettle_APaymentMethodThePhoneCannotSend_SettlesNothingAndAnswersTheSettlementWording(string paymentMethodJson)
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);
    var lines = string.Join(",", itemIds.Select(itemId => $$"""{"orderItemId":"{{itemId}}","paidPriceCents":350}"""));

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", JsonDocument.Parse($$"""{"lines":[{{lines}}],"paymentMethod":{{paymentMethodJson}}}""").RootElement);

    await AssertSettlementRefusedAndNothingStoredAsync(response);
  }

  [TestCase(350, "none")]
  [TestCase(0, "cash")]
  [TestCase(0, "card")]
  public async Task PostSettle_APaymentMethodThatDoesNotMatchTheAmount_SettlesNothingAndAnswersTheSettlementWording(int paidPriceCents, string paymentMethod)
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", new SettleItemsBody(itemIds.Select(itemId => new SettleLineBody(itemId, paidPriceCents, "Kapelle")).ToList(), paymentMethod));

    await AssertSettlementRefusedAndNothingStoredAsync(response);
  }

  [Test]
  public async Task PostCancel_AHandedOutSettledItemAndAnOpenOne_CancelsBothAnswersTheCashToHandBackAndTheTableShowsThemCancelled()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);
    await FulfillAndSettleAsync(itemIds[0]);

    using var response = await CancelAsync(_context.DeviceToken, itemIds);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var table = await SearchTableAsync(_context.World.TableId);
    var items = table.RootElement.GetProperty("orders")[0].GetProperty("items").EnumerateArray().ToList();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("cashToReturnCents").GetInt32(), Is.EqualTo(350));
                      Assert.That(items.Select(item => item.GetProperty("state").GetString()), Is.All.EqualTo("cancelled"));
                    });
  }

  [Test]
  public async Task PostCancel_NothingSelected_IsRefusedWithTheCancellationWording()
  {
    using var response = await CancelAsync(_context.DeviceToken, []);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.cancellation.cannotBeProcessed"));
                    });
  }

  [Test]
  public async Task PostCancel_ASelectionNamingOneItemTwice_IsRefusedWithTheCancellationWordingAndCancelsNothing()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await CancelAsync(_context.DeviceToken,
                                           [
                                             itemIds[0],
                                             itemIds[0]
                                           ]);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.cancellation.cannotBeProcessed"));
                    });
    Assert.That(await CancelledCountAsync(), Is.Zero);
  }

  [Test]
  public async Task PostCancel_ASelectionHoldingAnItemTheLaptopDoesNotKnow_IsRefusedAsUnknownAndCancelsNothing()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);

    using var response = await CancelAsync(_context.DeviceToken,
                                           [
                                             itemIds[0],
                                             Guid.NewGuid()
                                           ]);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.cancellation.unknownItem"));
                    });
    Assert.That(await CancelledCountAsync(), Is.Zero);
  }

  [Test]
  public async Task PostCancel_ASelectionSpanningTwoTables_IsRefusedWithTheCancellationWordingAndCancelsNothing()
  {
    var tableThreeId = await _context.SeedTableAsync("Tisch 3");
    IReadOnlyList<Guid> twelve = await PlaceOrderAsync(_context.World.TableId);
    IReadOnlyList<Guid> three = await PlaceOrderAsync(tableThreeId);

    using var response = await CancelAsync(_context.DeviceToken,
                                           [
                                             twelve[0],
                                             three[0]
                                           ]);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.cancellation.cannotBeProcessed"));
                    });
    Assert.That(await CancelledCountAsync(), Is.Zero);
  }

  [Test]
  public async Task PostCancel_TheSameItemASecondTime_IsRefusedAsAlreadyCancelledNamingIt()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);
    using var first = await CancelAsync(_context.DeviceToken, [itemIds[0]]);

    using var again = await CancelAsync(_context.DeviceToken, [itemIds[0]]);
    var body = JsonDocument.Parse(await again.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(again.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.cancellation.alreadyCancelled"));
                      Assert.That(body.RootElement.GetProperty("parameters").GetProperty("name").GetString(), Is.EqualTo("Bratwurst mit Brot"));
                    });
    Assert.That(await CancelledCountAsync(), Is.EqualTo(1));
  }

  [Test]
  public async Task PostCancel_TwoWaitersCancelTheSameItemAtTheSameMoment_CancelsItOnceAndRefusesTheOtherNamingIt()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);
    var colleagueToken = await _context.IssueSecondStaffTokenAsync("Bernd");

    HttpResponseMessage[] responses = await Task.WhenAll(CancelAsync(_context.DeviceToken, [itemIds[0]]), CancelAsync(colleagueToken, [itemIds[0]]));
    List<HttpStatusCode> statuses = responses.Select(response => response.StatusCode).ToList();
    var refused = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
    var body = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());

    foreach (var response in responses)
      response.Dispose();

    Assert.Multiple(() =>
                    {
                      Assert.That(statuses,
                                  Is.EquivalentTo(new[]
                                                  {
                                                    HttpStatusCode.OK,
                                                    HttpStatusCode.Conflict
                                                  }));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.cancellation.alreadyCancelled"));
                      Assert.That(body.RootElement.GetProperty("parameters").GetProperty("name").GetString(), Is.EqualTo("Bratwurst mit Brot"));
                    });
    Assert.That(await CancelledCountAsync(), Is.EqualTo(1));
  }

  [Test]
  public async Task PostCancel_ThenAHandOutOfTheSameItem_RefusesTheHandOutNamingTheItem()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);
    var kitchenToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);
    using var cancelled = await CancelAsync(_context.DeviceToken, [itemIds[0]]);

    using var handOut = await HandOutAsync(kitchenToken, [itemIds[0]]);
    var body = JsonDocument.Parse(await handOut.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(cancelled.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(handOut.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.station.itemWasCancelled"));
                      Assert.That(body.RootElement.GetProperty("parameters").GetProperty("name").GetString(), Is.EqualTo("Bratwurst mit Brot"));
                    });
    Assert.That(await HistoryOfAsync(itemIds[0]),
                Is.EqualTo(new[]
                           {
                             OrderItemState.Open,
                             OrderItemState.Cancelled
                           }));
  }

  [Test]
  public async Task PostCancel_AfterTheStationHandedTheItemOut_CancelsItAndTheCardNeedsNoAcknowledgement()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);
    var kitchenToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);
    using var handOut = await HandOutAsync(kitchenToken, itemIds);

    using var cancelled = await CancelAsync(_context.DeviceToken, [itemIds[0]]);
    using var queue = await _context.SendAsAsync(kitchenToken, HttpMethod.Get, "/api/station/orders");
    var queueBody = JsonDocument.Parse(await queue.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(handOut.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(cancelled.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(queueBody.RootElement.GetProperty("orders").GetArrayLength(), Is.Zero);
                    });
    Assert.That(await HistoryOfAsync(itemIds[0]),
                Is.EqualTo(new[]
                           {
                             OrderItemState.Open,
                             OrderItemState.HandedOut,
                             OrderItemState.Cancelled
                           }));
  }

  [Test]
  public async Task PostCancel_AndAHandOutOfTheSameItemAtTheSameMoment_EndInOneOfTheTwoOrdersAndNeverInBoth()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);
    var kitchenToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);

    HttpResponseMessage[] responses = await Task.WhenAll(CancelAsync(_context.DeviceToken, [itemIds[0]]), HandOutAsync(kitchenToken, [itemIds[0]]));
    var cancelStatus = responses[0].StatusCode;
    var handOutStatus = responses[1].StatusCode;

    foreach (var response in responses)
      response.Dispose();

    IReadOnlyList<OrderItemState> history = await HistoryOfAsync(itemIds[0]);
    IReadOnlyList<OrderItemState> cancelledFirst =
    [
      OrderItemState.Open,
      OrderItemState.Cancelled
    ];
    IReadOnlyList<OrderItemState> handedOutFirst =
    [
      OrderItemState.Open,
      OrderItemState.HandedOut,
      OrderItemState.Cancelled
    ];
    var expectedHandOutStatus = HttpStatusCode.OK;

    if (history.SequenceEqual(cancelledFirst))
      expectedHandOutStatus = HttpStatusCode.Conflict;

    Assert.Multiple(() =>
                    {
                      Assert.That(cancelStatus, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(history, Is.EqualTo(cancelledFirst).Or.EqualTo(handedOutFirst));
                      Assert.That(handOutStatus, Is.EqualTo(expectedHandOutStatus));
                    });
  }

  [Test]
  public async Task PostSettle_ACancelledItem_IsRefusedNamingItAndSettlesNothing()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);
    using var cancelled = await CancelAsync(_context.DeviceToken, [itemIds[1]]);

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", SettleBodyFor(itemIds, 350));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.settlement.itemWasCancelled"));
                      Assert.That(body.RootElement.GetProperty("parameters").GetProperty("name").GetString(), Is.EqualTo("Bratwurst mit Brot"));
                      Assert.That(stored.Select(item => item.SettledAtUtc), Is.All.Null);
                    });
  }

  private Task<HttpResponseMessage> CancelAsync(string deviceToken, IReadOnlyList<Guid> orderItemIds)
  {
    return _context.SendAsAsync(deviceToken, HttpMethod.Post, "/api/open-items/cancel", new CancelItemsBody(orderItemIds));
  }

  private Task<HttpResponseMessage> HandOutAsync(string stationToken, IReadOnlyList<Guid> orderItemIds)
  {
    return _context.SendAsAsync(stationToken, HttpMethod.Post, "/api/station/items/fulfill", new StationItemSelectionBody(orderItemIds));
  }

  private async Task<int> CancelledCountAsync()
  {
    await using var database = _context.Factory.CreateContext();

    return await database.OrderItemStatuses.CountAsync(status => status.Status == OrderItemState.Cancelled);
  }

  private async Task<IReadOnlyList<OrderItemState>> HistoryOfAsync(Guid orderItemId)
  {
    await using var database = _context.Factory.CreateContext();
    List<OrderItemStatus> statuses = await database.OrderItemStatuses.Where(status => status.OrderItemId == orderItemId).ToListAsync();
    List<OrderItemState> history = [];
    var current = statuses.Single(status => status.PreviousStatusId is null);

    while (true)
    {
      history.Add(current.Status);
      var next = statuses.SingleOrDefault(status => status.PreviousStatusId == current.Id);

      if (next is null)
        return history;

      current = next;
    }
  }

  private async Task AssertSettlementRefusedAndNothingStoredAsync(HttpResponseMessage response)
  {
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("ValidationFailed"));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("errors.settlement.cannotBeProcessed"));
                      Assert.That(stored.Select(item => item.SettledAtUtc), Is.All.Null);
                      Assert.That(stored.Select(item => item.PaymentMethod), Is.All.Null);
                    });
  }

  private SettleItemsBody SettleBodyFor(IReadOnlyList<Guid> orderItemIds, int? paidPriceCents, string? paymentNotice = null)
  {
    return new(orderItemIds.Select(orderItemId => new SettleLineBody(orderItemId, paidPriceCents, paymentNotice)).ToList(), paidPriceCents > 0 ? "cash" : "none");
  }

  [Test]
  public async Task SearchTable_TheOptionsOfKassieren_AnswerOnlyTheItemsNeitherPaidNorCancelled()
  {
    IReadOnlyList<Guid> firstItems = await PlaceOrderAsync(_context.World.TableId);
    IReadOnlyList<Guid> secondItems = await PlaceOrderAsync(_context.World.TableId);
    await FulfillAndSettleAsync(firstItems[0]);
    using (var cancelled = await _context.SendAsync(HttpMethod.Post, "/api/open-items/cancel", new CancelItemsBody([secondItems[0]])))
    {
      Assert.That(cancelled.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await _context.SendAsync(HttpMethod.Post, $"/api/open-items/tables/{_context.World.TableId}/search", new TableSearchBody(null, "notPaid", false));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var itemIds = body.RootElement.GetProperty("orders").EnumerateArray().SelectMany(order => order.GetProperty("items").EnumerateArray()).Select(item => item.GetProperty("orderItemId").GetGuid()).ToList();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(itemIds,
                                  Is.EquivalentTo(new[]
                                                  {
                                                    firstItems[1],
                                                    secondItems[1]
                                                  }));
                    });
  }

  [Test]
  public async Task SearchTable_PartlyHandedOutOrders_AnswerOnlyThoseOrdersWithAllTheirItems()
  {
    IReadOnlyList<Guid> partlyHandedOutItems = await PlaceOrderAsync(_context.World.TableId);
    await PlaceOrderAsync(_context.World.TableId);
    await FulfillAndSettleAsync(partlyHandedOutItems[0]);

    using var response = await _context.SendAsync(HttpMethod.Post, $"/api/open-items/tables/{_context.World.TableId}/search", new TableSearchBody("partiallyFulfilled", null, null));
    var orders = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("orders");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(orders.GetArrayLength(), Is.EqualTo(1));
                      Assert.That(orders[0].GetProperty("items").EnumerateArray().Select(item => item.GetProperty("orderItemId").GetGuid()), Is.EquivalentTo(partlyHandedOutItems));
                    });
  }

  [TestCase("""{"orderStatus":"eaten"}""")]
  [TestCase("""{"orderStatus":7}""")]
  [TestCase("""{"paymentStatus":7}""")]
  public async Task SearchTable_AnOptionOutsideItsValues_IsABadRequest(string json)
  {
    using var response = await _context.SendAsync(HttpMethod.Post, $"/api/open-items/tables/{_context.World.TableId}/search", JsonDocument.Parse(json).RootElement);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
  }

  [Test]
  public async Task SearchTable_AnItemCancelledAfterItsHandOut_AnswersTheOrdersCountsAndThatItWasHandedOutWhenCancelled()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);
    var kitchenToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);
    using (var handOut = await HandOutAsync(kitchenToken, [itemIds[0]]))
    {
      Assert.That(handOut.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using (var cancelled = await CancelAsync(_context.DeviceToken, [itemIds[0]]))
    {
      Assert.That(cancelled.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    var order = (await SearchTableAsync(_context.World.TableId)).RootElement.GetProperty("orders")[0];
    var cancelledItem = order.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("orderItemId").GetGuid() == itemIds[0]);
    var openItem = order.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("orderItemId").GetGuid() == itemIds[1]);

    Assert.Multiple(() =>
                    {
                      Assert.That(order.GetProperty("notCancelledItemCount").GetInt32(), Is.EqualTo(1));
                      Assert.That(order.GetProperty("handedOutItemCount").GetInt32(), Is.Zero);
                      Assert.That(cancelledItem.GetProperty("wasHandedOutWhenCancelled").GetBoolean(), Is.True);
                      Assert.That(openItem.GetProperty("wasHandedOutWhenCancelled").GetBoolean(), Is.False);
                    });
  }

  [Test]
  public async Task GetItemHistory_AnItemHandedOutPaidShortAndCancelled_AnswersEveryChangeWithItsAuthorAndThePaymentWithItsReason()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync(_context.World.TableId);
    var kitchenToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);
    using (var handOut = await HandOutAsync(kitchenToken, [itemIds[0]]))
    {
      Assert.That(handOut.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using (var settled = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", new SettleItemsBody([new(itemIds[0], 300, "Rabatt")], "cash")))
    {
      Assert.That(settled.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using (var cancelled = await CancelAsync(_context.DeviceToken, [itemIds[0]]))
    {
      Assert.That(cancelled.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await _context.SendAsync(HttpMethod.Get, $"/api/open-items/items/{itemIds[0]}/history");
    var history = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    var changes = history.GetProperty("statusChanges").EnumerateArray().ToList();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(changes.Select(change => change.GetProperty("state").GetString()),
                                  Is.EqualTo(new[]
                                             {
                                               "open",
                                               "handedOut",
                                               "cancelled"
                                             }));
                      Assert.That(changes.Select(change => change.GetProperty("staffMemberName").GetString()),
                                  Is.EqualTo(new[]
                                             {
                                               "Anna",
                                               null,
                                               "Anna"
                                             }));
                      Assert.That(changes.Select(change => change.GetProperty("stationName").GetString()),
                                  Is.EqualTo(new[]
                                             {
                                               null,
                                               "Kueche",
                                               null
                                             }));
                      Assert.That(changes.Select(change => change.GetProperty("changedAtUtc").ValueKind), Is.All.EqualTo(JsonValueKind.String));
                      Assert.That(history.GetProperty("settledAtUtc").ValueKind, Is.EqualTo(JsonValueKind.String));
                      Assert.That(history.GetProperty("chargedPriceCents").GetInt32(), Is.EqualTo(300));
                      Assert.That(history.GetProperty("paymentMethod").GetString(), Is.EqualTo("cash"));
                      Assert.That(history.GetProperty("settledByStaffMemberName").GetString(), Is.EqualTo("Anna"));
                      Assert.That(history.GetProperty("paymentNotice").GetString(), Is.EqualTo("Rabatt"));
                    });
  }

  [Test]
  public async Task GetItemHistory_AnItemTheLaptopDoesNotKnow_IsRefusedWithTheItemHistoryWording()
  {
    using var response = await _context.SendAsync(HttpMethod.Get, $"/api/open-items/items/{Guid.NewGuid()}/history");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.GetProperty("code").GetString(), Is.EqualTo("UnprocessableEntity"));
                      Assert.That(body.GetProperty("messageKey").GetString(), Is.EqualTo("errors.itemHistory.unknownItem"));
                    });
  }

  [Test]
  public async Task GetItemHistory_WithoutADeviceToken_IsRefused()
  {
    using HttpRequestMessage request = new(HttpMethod.Get, $"/api/open-items/items/{Guid.NewGuid()}/history");
    using var response = await _context.Client.SendAsync(request);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  private async Task FulfillAndSettleAsync(Guid orderItemId)
  {
    await using var database = _context.Factory.CreateContext();
    await new StoredItemHandOut().HandOutItemAsync(database, orderItemId);
    var item = await database.OrderItems.FirstAsync(candidate => candidate.Id == orderItemId);

    item.SettledAtUtc = DateTime.UtcNow;
    item.SettledByStaffMemberId = _context.World.StaffMemberId;
    item.ChargedPriceCents = item.UnitPriceCents;

    await database.SaveChangesAsync();
  }

  private async Task<JsonDocument> SearchTableAsync(Guid tableId)
  {
    using var response = await _context.SendAsync(HttpMethod.Post, $"/api/open-items/tables/{tableId}/search", new TableSearchBody(null, null, null));

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
  }

  private List<JsonValueKind> SettledAtUtcKindsIn(JsonDocument table)
  {
    return table.RootElement.GetProperty("orders").EnumerateArray().SelectMany(order => order.GetProperty("items").EnumerateArray()).Select(item => item.GetProperty("settledAtUtc").ValueKind).ToList();
  }

  private async Task RenameTheTableAsync(Guid tableId, string name)
  {
    await using var database = _context.Factory.CreateContext();
    var table = await database.Tables.SingleAsync(candidate => candidate.Id == tableId);

    table.Name = name;

    await database.SaveChangesAsync();
  }

  private async Task<IReadOnlyList<Guid>> PlaceOrderAsync(Guid tableId)
  {
    OrderBody order = new(Guid.NewGuid(),
                          tableId,
                          [
                            new(_context.World.BratwurstItemId, 350, null, null),
                            new(_context.World.BratwurstItemId, 350, null, null)
                          ]);

    using var response = await _context.PostOrderAsync(order);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("stationOrders").EnumerateArray().SelectMany(stationOrder => stationOrder.GetProperty("itemIds").EnumerateArray()).Select(itemId => itemId.GetGuid()).ToList();
  }
}
