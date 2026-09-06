using System.Net;
using System.Text.Json;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

public sealed record StationItemStatusBody(IReadOnlyList<Guid> OrderItemIds, string Status);

[TestFixture]
public sealed class StationQueueEndpointsTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync();
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
  public async Task GetStationOrders_StationTablet_ShowsOnlyTheSlicesOfItsOwnStation()
  {
    await PlaceOrderAcrossBothStationsAsync("Tisch 3");

    using var response = await _context.SendAsAsync(_kitchenToken, HttpMethod.Get, "/api/station/orders");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var slices = body.RootElement.GetProperty("slices");

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("station").GetProperty("id").GetGuid(),
                                  Is.EqualTo(_context.World.KitchenStationId));
                      Assert.That(body.RootElement.GetProperty("station").GetProperty("name").GetString(),
                                  Is.EqualTo("Kueche"));
                      Assert.That(slices.GetArrayLength(), Is.EqualTo(1));
                      Assert.That(slices[0].GetProperty("globalOrderNumber").GetInt32(), Is.EqualTo(1));
                      Assert.That(slices[0].GetProperty("stationOrderNumber").GetInt32(), Is.EqualTo(1));
                      Assert.That(slices[0].GetProperty("tableName").GetString(), Is.EqualTo("Tisch 3"));
                      Assert.That(slices[0].GetProperty("deliveryMode").GetString(), Is.EqualTo("together"));
                      Assert.That(slices[0].GetProperty("items").GetArrayLength(), Is.EqualTo(2));
                      Assert.That(slices[0].GetProperty("items")[0].GetProperty("itemName").GetString(),
                                  Is.EqualTo("Bratwurst mit Brot"));
                      Assert.That(slices[0].GetProperty("items")[0].GetProperty("productionStatus").GetString(),
                                  Is.EqualTo("waiting"));
                    });
  }

  [Test]
  public async Task GetStationOrders_SliceWhoseItemsAreAllFinished_LeavesItOutOfTheQueue()
  {
    IReadOnlyList<Guid> kitchenItemIds = await KitchenItemIdsOfAsync(await PlaceOrderAcrossBothStationsAsync("Tisch 3"));

    using (var advanced = await AdvanceAsync(_kitchenToken, kitchenItemIds, "finished"))
    {
      Assert.That(advanced.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await _context.SendAsAsync(_kitchenToken, HttpMethod.Get, "/api/station/orders");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.That(body.RootElement.GetProperty("slices").GetArrayLength(), Is.Zero);
  }

  [Test]
  public async Task PostItemStatus_ItemsOfItsOwnStation_MovesThemAndAnswersWithTheTableAndTheSlices()
  {
    IReadOnlyList<Guid> kitchenItemIds = await KitchenItemIdsOfAsync(await PlaceOrderAcrossBothStationsAsync("Tisch 3"));

    using var response = await AdvanceAsync(_kitchenToken, kitchenItemIds, "inProduction");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems
                                           .Where(item => kitchenItemIds.Contains(item.Id))
                                           .ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("tableName").GetString(), Is.EqualTo("Tisch 3"));
                      Assert.That(body.RootElement.GetProperty("slices").GetArrayLength(), Is.EqualTo(1));
                      Assert.That(body.RootElement.GetProperty("slices")[0].GetProperty("items")[0]
                                      .GetProperty("productionStatus").GetString(),
                                  Is.EqualTo("inProduction"));
                      Assert.That(stored.Select(item => item.ProductionStatus),
                                  Is.All.EqualTo(ProductionStatus.InProduction));
                    });
  }

  [Test]
  public async Task PostItemStatus_ItemsMoved_AppendsOneRowPerItemToTheStatusChangeLog()
  {
    IReadOnlyList<Guid> kitchenItemIds = await KitchenItemIdsOfAsync(await PlaceOrderAcrossBothStationsAsync("Tisch 3"));

    using (var advanced = await AdvanceAsync(_kitchenToken, kitchenItemIds, "finished"))
    {
      Assert.That(advanced.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    await using var database = _context.Factory.CreateContext();
    List<OrderItemStatusChange> changes = await database.OrderItemStatusChanges
                                                        .Where(change => kitchenItemIds.Contains(change.OrderItemId))
                                                        .ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(changes.Count(change => change.Status == ProductionStatus.Waiting), Is.EqualTo(2));
                      Assert.That(changes.Count(change => change.Status == ProductionStatus.Finished), Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task PostItemStatus_AnItemOfAnotherStation_ChangesNothingAtAll()
  {
    var placed = await PlaceOrderAcrossBothStationsAsync("Tisch 3");
    IReadOnlyList<Guid> kitchenItemIds = await KitchenItemIdsOfAsync(placed);
    IReadOnlyList<Guid> barItemIds = await BarItemIdsOfAsync(placed);

    using var response = await AdvanceAsync(_kitchenToken, [.. kitchenItemIds, .. barItemIds], "inProduction");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("station.itemNotAtThisStation"));
                      Assert.That(stored.Select(item => item.ProductionStatus),
                                  Is.All.EqualTo(ProductionStatus.Waiting));
                    });
  }

  [Test]
  public async Task PostItemStatus_NothingSelected_IsRefusedAsAValidationFailure()
  {
    await PlaceOrderAcrossBothStationsAsync("Tisch 3");

    using var response = await AdvanceAsync(_kitchenToken, [], "inProduction");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("station.noItemsSelected"));
                    });
  }

  [Test]
  public async Task PostItemStatus_AStatusTheItemHasAlreadyPassed_IsRefused()
  {
    IReadOnlyList<Guid> kitchenItemIds = await KitchenItemIdsOfAsync(await PlaceOrderAcrossBothStationsAsync("Tisch 3"));

    using (var finished = await AdvanceAsync(_kitchenToken, kitchenItemIds, "finished"))
    {
      Assert.That(finished.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await AdvanceAsync(_kitchenToken, kitchenItemIds, "inProduction");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("station.statusAlreadyPassed"));
                    });
  }

  [Test]
  public async Task PostItemStatus_TheStatusTheItemsAlreadyHold_IsAcceptedAndStillNamesTheTableAndTheSlices()
  {
    IReadOnlyList<Guid> kitchenItemIds = await KitchenItemIdsOfAsync(await PlaceOrderAcrossBothStationsAsync("Tisch 3"));

    using (var first = await AdvanceAsync(_kitchenToken, kitchenItemIds, "inProduction"))
    {
      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var response = await AdvanceAsync(_kitchenToken, kitchenItemIds, "inProduction");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("tableName").GetString(), Is.EqualTo("Tisch 3"));
                      Assert.That(body.RootElement.GetProperty("slices").GetArrayLength(), Is.EqualTo(1));
                      Assert.That(body.RootElement.GetProperty("slices")[0].GetProperty("items")[0]
                                      .GetProperty("productionStatus").GetString(),
                                  Is.EqualTo("inProduction"));
                    });
  }

  [Test]
  public async Task PostItemStatus_TheStatusTheItemsAlreadyHold_WritesNoSecondRowIntoTheStatusChangeLog()
  {
    IReadOnlyList<Guid> kitchenItemIds = await KitchenItemIdsOfAsync(await PlaceOrderAcrossBothStationsAsync("Tisch 3"));

    using (var first = await AdvanceAsync(_kitchenToken, kitchenItemIds, "inProduction"))
    {
      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using (var second = await AdvanceAsync(_kitchenToken, kitchenItemIds, "inProduction"))
    {
      Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    await using var database = _context.Factory.CreateContext();
    List<OrderItemStatusChange> changes = await database.OrderItemStatusChanges
                                                        .Where(change => kitchenItemIds.Contains(change.OrderItemId))
                                                        .ToListAsync();

    Assert.That(changes.Count(change => change.Status == ProductionStatus.InProduction), Is.EqualTo(2));
  }

  [Test]
  public async Task GetStationOrders_TheOtherStationTablet_SeesOnlyItsOwnSlice()
  {
    await PlaceOrderAcrossBothStationsAsync("Tisch 3");

    using var response = await _context.SendAsAsync(_barToken, HttpMethod.Get, "/api/station/orders");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var slices = body.RootElement.GetProperty("slices");

    Assert.Multiple(() =>
                    {
                      Assert.That(slices.GetArrayLength(), Is.EqualTo(1));
                      Assert.That(slices[0].GetProperty("items")[0].GetProperty("itemName").GetString(),
                                  Is.EqualTo("Bier"));
                    });
  }

  private Task<HttpResponseMessage> AdvanceAsync(string deviceToken,
                                                 IReadOnlyList<Guid> orderItemIds,
                                                 string status)
  {
    return _context.SendAsAsync(deviceToken,
                                HttpMethod.Post,
                                "/api/station/items/status",
                                new StationItemStatusBody(orderItemIds, status));
  }

  private async Task<Guid> PlaceOrderAcrossBothStationsAsync(string tableName)
  {
    OrderBody order = new(Guid.NewGuid(),
                          tableName,
                          null,
                          [
                            new(_context.World.BratwurstItemId, 350, null, null),
                            new(_context.World.BratwurstItemId, 350, null, null),
                            new(_context.World.BeerItemId, 300, null, null)
                          ]);

    using var response = await _context.PostOrderAsync(order);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("orderId").GetGuid();
  }

  private Task<IReadOnlyList<Guid>> KitchenItemIdsOfAsync(Guid orderId)
  {
    return ItemIdsOfAsync(orderId, _context.World.KitchenStationId);
  }

  private Task<IReadOnlyList<Guid>> BarItemIdsOfAsync(Guid orderId)
  {
    return ItemIdsOfAsync(orderId, _context.World.BarStationId);
  }

  private async Task<IReadOnlyList<Guid>> ItemIdsOfAsync(Guid orderId, Guid stationId)
  {
    await using var database = _context.Factory.CreateContext();

    var stationOrder = await database.StationOrders
                                     .SingleAsync(candidate => candidate.OrderId == orderId
                                                               && candidate.StationId == stationId);

    return await database.OrderItems
                         .Where(item => item.StationOrderId == stationOrder.Id)
                         .Select(item => item.Id)
                         .ToListAsync();
  }
}
