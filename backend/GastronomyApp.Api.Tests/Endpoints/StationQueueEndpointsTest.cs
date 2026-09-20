using System.Net;
using System.Text.Json;
using GastronomyApp.Api.Tests.TestSupport;
using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class StationQueueEndpointsTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContextBuilder().StartAsync();
    _kitchenToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);
    _barToken = await _context.IssueStationTokenAsync(_context.World.BarStationId);
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;
  private string _kitchenToken = null!;
  private string _barToken = null!;

  [Test]
  public async Task GetStationOrders_StationTablet_ShowsOnlyTheStationOrdersOfItsOwnStation()
  {
    await PlaceOrderAcrossBothStationsAsync("Tisch 3");

    using var response = await _context.SendAsAsync(_kitchenToken, HttpMethod.Get, "/api/station/orders");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var orders = body.RootElement.GetProperty("orders");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("station").GetProperty("id").GetGuid(), Is.EqualTo(_context.World.KitchenStationId));
                      Assert.That(body.RootElement.GetProperty("station").GetProperty("name").GetString(), Is.EqualTo("Kueche"));
                      Assert.That(orders.GetArrayLength(), Is.EqualTo(1));
                      Assert.That(orders[0].GetProperty("globalOrderNumber").GetInt32(), Is.EqualTo(1));
                      Assert.That(orders[0].GetProperty("stationOrderNumber").GetInt32(), Is.EqualTo(1));
                      Assert.That(orders[0].GetProperty("tableName").GetString(), Is.EqualTo("Tisch 3"));
                      Assert.That(orders[0].GetProperty("deliveryMode").GetString(), Is.EqualTo("together"));
                      Assert.That(orders[0].GetProperty("itemCount").GetInt32(), Is.EqualTo(2));
                      Assert.That(orders[0].GetProperty("fulfilledItemCount").GetInt32(), Is.Zero);
                      Assert.That(orders[0].GetProperty("items").GetArrayLength(), Is.EqualTo(2));
                      Assert.That(orders[0].GetProperty("items")[0].GetProperty("itemName").GetString(), Is.EqualTo("Bratwurst mit Brot"));
                      Assert.That(orders[0].GetProperty("items")[0].GetProperty("fulfilledAtUtc").ValueKind, Is.EqualTo(JsonValueKind.Null));
                    });
  }

  [Test]
  public async Task GetStationOrders_TwoStationOrdersAtTheKitchen_ListsBothWithOnlyTheAsItComesOneInBothColumns()
  {
    await PlaceOrderAcrossBothStationsAsync("Tisch 3", _context.World.KitchenStationId);
    await PlaceOrderAcrossBothStationsAsync("Tisch 4");

    using var response = await _context.SendAsAsync(_kitchenToken, HttpMethod.Get, "/api/station/orders");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var orders = body.RootElement.GetProperty("orders");
    var asItComes = body.RootElement.GetProperty("asItComes");

    var asItComesStationOrder = orders.EnumerateArray().Single(stationOrder => stationOrder.GetProperty("tableName").GetString() == "Tisch 3");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(orders.GetArrayLength(), Is.EqualTo(2));
                      Assert.That(asItComes.GetArrayLength(), Is.EqualTo(1));
                      Assert.That(asItComes[0].GetProperty("stationOrderId").GetGuid(), Is.EqualTo(asItComesStationOrder.GetProperty("stationOrderId").GetGuid()));
                      Assert.That(orders[0].GetProperty("stationOrderNumber").GetInt32(), Is.EqualTo(1));
                      Assert.That(orders[1].GetProperty("stationOrderNumber").GetInt32(), Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task GetStationOrders_TheOtherStationTablet_SeesOnlyItsOwnStationOrder()
  {
    await PlaceOrderAcrossBothStationsAsync("Tisch 3");

    using var response = await _context.SendAsAsync(_barToken, HttpMethod.Get, "/api/station/orders");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var orders = body.RootElement.GetProperty("orders");

    Assert.Multiple(() =>
                    {
                      Assert.That(orders.GetArrayLength(), Is.EqualTo(1));
                      Assert.That(orders[0].GetProperty("items")[0].GetProperty("itemName").GetString(), Is.EqualTo("Bier"));
                    });
  }

  [Test]
  public async Task PostItemFulfill_OneItemOfItsOwnStation_StampsItAndAnswersWithTheQueue()
  {
    IReadOnlyList<Guid> kitchenItemIds = await ReadKitchenItemIdsAsync(await PlaceOrderAcrossBothStationsAsync("Tisch 3"));

    using var response = await FulfillAsync(_kitchenToken, [kitchenItemIds[0]]);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var orders = body.RootElement.GetProperty("orders");

    await using var database = _context.Factory.CreateContext();
    var stored = await database.OrderItems.SingleAsync(item => item.Id == kitchenItemIds[0]);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("station").GetProperty("id").GetGuid(), Is.EqualTo(_context.World.KitchenStationId));
                      Assert.That(orders.GetArrayLength(), Is.EqualTo(1));
                      Assert.That(orders[0].GetProperty("itemCount").GetInt32(), Is.EqualTo(2));
                      Assert.That(orders[0].GetProperty("fulfilledItemCount").GetInt32(), Is.EqualTo(1));
                      Assert.That(stored.FulfilledAtUtc, Is.Not.Null);
                    });
  }

  [Test]
  public async Task PostItemFulfill_EveryItemOfAStationOrder_LeavesItOutOfBothLists()
  {
    var orderId = await PlaceOrderAcrossBothStationsAsync("Tisch 3", _context.World.KitchenStationId);
    IReadOnlyList<Guid> kitchenItemIds = await ReadKitchenItemIdsAsync(orderId);

    using var response = await FulfillAsync(_kitchenToken, kitchenItemIds);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("orders").GetArrayLength(), Is.Zero);
                      Assert.That(body.RootElement.GetProperty("asItComes").GetArrayLength(), Is.Zero);
                    });
  }

  [Test]
  public async Task PostItemFulfill_OneItemOfAStationOrder_ReportsThePartialCountAndKeepsEveryLine()
  {
    IReadOnlyList<Guid> kitchenItemIds = await ReadKitchenItemIdsAsync(await PlaceOrderAcrossBothStationsAsync("Tisch 3"));

    using (var fulfilled = await FulfillAsync(_kitchenToken, [kitchenItemIds[0]]))
    {
      Assert.That(fulfilled.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await _context.SendAsAsync(_kitchenToken, HttpMethod.Get, "/api/station/orders");
    var stationOrder = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("orders")[0];

    Assert.Multiple(() =>
                    {
                      Assert.That(stationOrder.GetProperty("itemCount").GetInt32(), Is.EqualTo(2));
                      Assert.That(stationOrder.GetProperty("fulfilledItemCount").GetInt32(), Is.EqualTo(1));
                      Assert.That(stationOrder.GetProperty("items").GetArrayLength(), Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task PostItemUnfulfill_AJustFulfilledItem_ClearsTheTimestampAgain()
  {
    IReadOnlyList<Guid> kitchenItemIds = await ReadKitchenItemIdsAsync(await PlaceOrderAcrossBothStationsAsync("Tisch 3"));

    using (var fulfilled = await FulfillAsync(_kitchenToken, [kitchenItemIds[0]]))
    {
      Assert.That(fulfilled.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await UnfulfillAsync(_kitchenToken, [kitchenItemIds[0]]);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    await using var database = _context.Factory.CreateContext();
    var stored = await database.OrderItems.SingleAsync(item => item.Id == kitchenItemIds[0]);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("orders")[0].GetProperty("fulfilledItemCount").GetInt32(), Is.Zero);
                      Assert.That(stored.FulfilledAtUtc, Is.Null);
                    });
  }

  [Test]
  public async Task PostItemUnfulfill_AnOpenItem_IsRefusedAndChangesNothing()
  {
    IReadOnlyList<Guid> kitchenItemIds = await ReadKitchenItemIdsAsync(await PlaceOrderAcrossBothStationsAsync("Tisch 3"));

    using var response = await UnfulfillAsync(_kitchenToken, [kitchenItemIds[0]]);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("ItemNotFulfilled"));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("station.changeNotSaved"));
                    });
  }

  [Test]
  public async Task PostItemFulfill_AnItemOfAnotherStation_IsRefusedAndChangesNothing()
  {
    var placed = await PlaceOrderAcrossBothStationsAsync("Tisch 3");
    IReadOnlyList<Guid> kitchenItemIds = await ReadKitchenItemIdsAsync(placed);
    IReadOnlyList<Guid> barItemIds = await ReadBarItemIdsAsync(placed);

    using var response = await FulfillAsync(_kitchenToken, kitchenItemIds.Concat(barItemIds).ToList());
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("station.itemNotAtThisStation"));
                      Assert.That(stored.Select(item => item.FulfilledAtUtc), Is.All.Null);
                    });
  }

  [Test]
  public async Task PostItemFulfill_NothingSelected_IsRefusedAsAValidationFailure()
  {
    await PlaceOrderAcrossBothStationsAsync("Tisch 3");

    using var response = await FulfillAsync(_kitchenToken, []);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("station.noItemsSelected"));
                    });
  }

  [Test]
  public async Task PostHide_AnAsItComesStationOrder_LeavesItInOrdersAndOutOfAsItComes()
  {
    var orderId = await PlaceOrderAcrossBothStationsAsync("Tisch 3", _context.World.KitchenStationId);
    var stationOrderId = await LoadStationOrderIdAsync(orderId, _context.World.KitchenStationId);

    using var response = await HideAsync(_kitchenToken, stationOrderId);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("orders").GetArrayLength(), Is.EqualTo(1));
                      Assert.That(body.RootElement.GetProperty("orders")[0].GetProperty("isHiddenFromAsItComesQueue").GetBoolean(), Is.True);
                      Assert.That(body.RootElement.GetProperty("asItComes").GetArrayLength(), Is.Zero);
                    });

    using var queue = await _context.SendAsAsync(_kitchenToken, HttpMethod.Get, "/api/station/orders");
    var queueBody = JsonDocument.Parse(await queue.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(queueBody.RootElement.GetProperty("orders").GetArrayLength(), Is.EqualTo(1));
                      Assert.That(queueBody.RootElement.GetProperty("asItComes").GetArrayLength(), Is.Zero);
                    });
  }

  [Test]
  public async Task PostHide_ATogetherStationOrder_IsRefusedAndLeavesTheFlagOff()
  {
    var orderId = await PlaceOrderAcrossBothStationsAsync("Tisch 3");
    var stationOrderId = await LoadStationOrderIdAsync(orderId, _context.World.KitchenStationId);

    using var response = await HideAsync(_kitchenToken, stationOrderId);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    await using var database = _context.Factory.CreateContext();
    var stored = await database.StationOrders.SingleAsync(stationOrder => stationOrder.Id == stationOrderId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("CannotHideTogetherOrder"));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("station.changeNotSaved"));
                      Assert.That(stored.IsHiddenFromAsItComesQueue, Is.False);
                    });
  }

  [Test]
  public async Task PostHide_AStationOrderOfAnotherStation_IsRefusedAndLeavesItAlone()
  {
    var orderId = await PlaceOrderAcrossBothStationsAsync("Tisch 3", _context.World.BarStationId);
    var barStationOrderId = await LoadStationOrderIdAsync(orderId, _context.World.BarStationId);

    using var response = await HideAsync(_kitchenToken, barStationOrderId);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    await using var database = _context.Factory.CreateContext();
    var stored = await database.StationOrders.SingleAsync(stationOrder => stationOrder.Id == barStationOrderId);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("UnprocessableEntity"));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("station.orderNotAtThisStation"));
                      Assert.That(stored.IsHiddenFromAsItComesQueue, Is.False);
                    });
  }

  [Test]
  public async Task GetFulfilledOrders_APartialAndACompleteStationOrder_ListsBothWithEveryLine()
  {
    var partialOrderId = await PlaceOrderAcrossBothStationsAsync("Tisch 3");
    var completeOrderId = await PlaceOrderAcrossBothStationsAsync("Tisch 4");
    IReadOnlyList<Guid> partialItemIds = await ReadKitchenItemIdsAsync(partialOrderId);
    IReadOnlyList<Guid> completeItemIds = await ReadKitchenItemIdsAsync(completeOrderId);

    using (var partial = await FulfillAsync(_kitchenToken, [partialItemIds[0]]))
    {
      Assert.That(partial.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using (var complete = await FulfillAsync(_kitchenToken, completeItemIds))
    {
      Assert.That(complete.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await _context.SendAsAsync(_kitchenToken, HttpMethod.Get, "/api/station/orders/fulfilled");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var stationOrders = body.RootElement.GetProperty("stationOrders");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(stationOrders.GetArrayLength(), Is.EqualTo(2));
                      Assert.That(stationOrders[0].GetProperty("tableName").GetString(), Is.EqualTo("Tisch 3"));
                      Assert.That(stationOrders[0].GetProperty("fulfilledItemCount").GetInt32(), Is.EqualTo(1));
                      Assert.That(stationOrders[0].GetProperty("items").GetArrayLength(), Is.EqualTo(2));
                      Assert.That(stationOrders[1].GetProperty("tableName").GetString(), Is.EqualTo("Tisch 4"));
                      Assert.That(stationOrders[1].GetProperty("fulfilledItemCount").GetInt32(), Is.EqualTo(2));
                    });
  }

  private Task<HttpResponseMessage> FulfillAsync(string deviceToken, IReadOnlyList<Guid> orderItemIds)
  {
    return _context.SendAsAsync(deviceToken, HttpMethod.Post, "/api/station/items/fulfill", new StationItemSelectionBody(orderItemIds));
  }

  private Task<HttpResponseMessage> UnfulfillAsync(string deviceToken, IReadOnlyList<Guid> orderItemIds)
  {
    return _context.SendAsAsync(deviceToken, HttpMethod.Post, "/api/station/items/unfulfill", new StationItemSelectionBody(orderItemIds));
  }

  private Task<HttpResponseMessage> HideAsync(string deviceToken, Guid stationOrderId)
  {
    return _context.SendAsAsync(deviceToken, HttpMethod.Post, $"/api/station/orders/{stationOrderId}/hide");
  }

  private async Task<Guid> PlaceOrderAcrossBothStationsAsync(string tableName, Guid? asItComesStationId = null)
  {
    IReadOnlyList<DeliveryModeBody> deliveryModes = [];

    if (asItComesStationId is not null)
      deliveryModes = [new(asItComesStationId.Value, "asItComes")];

    OrderWithDeliveryModesBody order = new(Guid.NewGuid(),
                                           tableName,
                                           [
                                             new(_context.World.BratwurstItemId, 350, null, null),
                                             new(_context.World.BratwurstItemId, 350, null, null),
                                             new(_context.World.BeerItemId, 300, null, null)
                                           ],
                                           deliveryModes);

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/orders", order);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("orderId").GetGuid();
  }

  private Task<IReadOnlyList<Guid>> ReadKitchenItemIdsAsync(Guid orderId)
  {
    return ReadItemIdsAsync(orderId, _context.World.KitchenStationId);
  }

  private Task<IReadOnlyList<Guid>> ReadBarItemIdsAsync(Guid orderId)
  {
    return ReadItemIdsAsync(orderId, _context.World.BarStationId);
  }

  private async Task<IReadOnlyList<Guid>> ReadItemIdsAsync(Guid orderId, Guid stationId)
  {
    await using var database = _context.Factory.CreateContext();

    var stationOrderId = await LoadStationOrderIdAsync(orderId, stationId);

    return await database.OrderItems.Where(item => item.StationOrderId == stationOrderId).Select(item => item.Id).ToListAsync();
  }

  private async Task<Guid> LoadStationOrderIdAsync(Guid orderId, Guid stationId)
  {
    await using var database = _context.Factory.CreateContext();

    return await database.StationOrders.Where(stationOrder => stationOrder.OrderId == orderId && stationOrder.StationId == stationId).Select(stationOrder => stationOrder.Id).SingleAsync();
  }
}
