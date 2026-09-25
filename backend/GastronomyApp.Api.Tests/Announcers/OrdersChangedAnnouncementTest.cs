using System.Net;
using GastronomyApp.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Announcers;

[TestFixture]
public sealed class OrdersChangedAnnouncementTest
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
    foreach (var listener in _listeners)
      await listener.DisposeAsync();

    _listeners.Clear();
    await _context.DisposeAsync();
  }

  private readonly List<HubEventListener> _listeners = [];
  private readonly TimeSpan _patience = TimeSpan.FromSeconds(10);
  private readonly TimeSpan _settleTime = TimeSpan.FromMilliseconds(500);

  private string _barToken = null!;
  private OrderTestContext _context = null!;
  private string _kitchenToken = null!;

  [Test]
  public async Task PostOrder_AnOrderForTwoStations_TellsThePhoneTheAdminAndBothStationsOnceAndSendsTheCounterChangeOnce()
  {
    await using HubEventListener configurationListener = new(new(_context.Factory.BaseAddress, "hub"), "ConfigurationChanged");
    await configurationListener.StartAsync();
    await ListenAsync(_context.DeviceToken, null, _kitchenToken, _barToken);

    using var response = await _context.PostOrderAsync(new(Guid.NewGuid(),
                                                           "Tisch 12",
                                                           [
                                                             new(_context.World.BratwurstItemId, 350, null, null),
                                                             new(_context.World.BeerItemId, 300, null, null)
                                                           ]));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    await AssertEveryListenerHeardOnceAsync();
    Assert.That(configurationListener.HeardCount, Is.EqualTo(1), "Placing an order moves the festival and station counters, which is one configuration change per request.");
  }

  [Test]
  public async Task FulfillItems_TheKitchenHandsOutItsItems_TellsThePhoneTheAdminAndTheKitchenOnce()
  {
    await PlaceTheKitchenOrderAsync();
    List<Guid> itemIds = await KitchenItemIdsAsync();
    await ListenAsync(_context.DeviceToken, null, _kitchenToken);

    using var response = await _context.SendAsAsync(_kitchenToken, HttpMethod.Post, "/api/station/items/fulfill", new StationItemSelectionBody(itemIds));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    await AssertEveryListenerHeardOnceAsync();
  }

  [Test]
  public async Task UnfulfillItems_TheKitchenPutsItsItemsBack_TellsThePhoneTheAdminAndTheKitchenOnce()
  {
    await PlaceTheKitchenOrderAsync();
    List<Guid> itemIds = await KitchenItemIdsAsync();

    using (var fulfilled = await _context.SendAsAsync(_kitchenToken, HttpMethod.Post, "/api/station/items/fulfill", new StationItemSelectionBody(itemIds)))
    {
      Assert.That(fulfilled.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    await ListenAsync(_context.DeviceToken, null, _kitchenToken);

    using var response = await _context.SendAsAsync(_kitchenToken, HttpMethod.Post, "/api/station/items/unfulfill", new StationItemSelectionBody(itemIds));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    await AssertEveryListenerHeardOnceAsync();
  }

  [Test]
  public async Task HideStationOrder_AnAsItComesOrder_TellsThePhoneTheAdminAndTheKitchenOnce()
  {
    using (var placed = await _context.SendAsync(HttpMethod.Post,
                                                 "/api/orders",
                                                 new OrderWithDeliveryModesBody(Guid.NewGuid(),
                                                                                "Tisch 12",
                                                                                [new(_context.World.BratwurstItemId, 350, null, null)],
                                                                                [new(_context.World.KitchenStationId, "asItComes")])))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    Guid stationOrderId;

    await using (var database = _context.Factory.CreateContext())
    {
      stationOrderId = await database.StationOrders.Select(stationOrder => stationOrder.Id).SingleAsync();
    }

    await ListenAsync(_context.DeviceToken, null, _kitchenToken);

    using var response = await _context.SendAsAsync(_kitchenToken, HttpMethod.Post, $"/api/station/orders/{stationOrderId}/hide");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    await AssertEveryListenerHeardOnceAsync();
  }

  [Test]
  public async Task SettleItems_TheWaiterSettlesATable_TellsThePhoneTheAdminAndTheKitchenOnce()
  {
    await PlaceTheKitchenOrderAsync();
    List<Guid> itemIds = await KitchenItemIdsAsync();
    await ListenAsync(_context.DeviceToken, null, _kitchenToken);

    using var response = await _context.SendAsync(HttpMethod.Post, "/api/open-items/settle", new SettleItemsBody(itemIds.Select(itemId => new SettleLineBody(itemId, 350)).ToList()));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    await AssertEveryListenerHeardOnceAsync();
  }

  private async Task ListenAsync(params string?[] deviceTokens)
  {
    foreach (var deviceToken in deviceTokens)
    {
      HubEventListener listener = new(new(_context.Factory.BaseAddress, deviceToken is null ? "hub" : $"hub?access_token={deviceToken}"), "OrdersChanged");
      _listeners.Add(listener);
      await listener.StartAsync();
    }
  }

  private async Task PlaceTheKitchenOrderAsync()
  {
    using var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
  }

  private async Task<List<Guid>> KitchenItemIdsAsync()
  {
    await using var database = _context.Factory.CreateContext();

    return await database.OrderItems.Where(item => item.StationOrder.StationId == _context.World.KitchenStationId).Select(item => item.Id).ToListAsync();
  }

  private async Task AssertEveryListenerHeardOnceAsync()
  {
    Task heardByAll = Task.WhenAll(_listeners.Select(listener => listener.FirstHeard));

    Assert.That(await Task.WhenAny(heardByAll, Task.Delay(_patience)), Is.SameAs(heardByAll), "Every screen that shows the order must be told that it changed.");

    await Task.Delay(_settleTime);

    Assert.That(_listeners.Select(listener => listener.HeardCount), Is.All.EqualTo(1));
  }
}
