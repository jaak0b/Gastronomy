using System.Net;
using System.Text.Json;
using GastronomyApp.Api.Tests.Endpoints;
using Microsoft.AspNetCore.SignalR.Client;

namespace GastronomyApp.Api.Tests.Hub;

[TestFixture]
public sealed class StationGroupTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync();
    _kitchenToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

  private OrderTestContext _context = null!;
  private string _kitchenToken = null!;

  [Test]
  public async Task PlaceOrder_ASliceForThatStation_ReachesItsTabletAndNotTheWaiterPhones()
  {
    TaskCompletionSource<Guid> heardByTheTablet = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource<Guid> heardByThePhone = new(TaskCreationOptions.RunContinuationsAsynchronously);

    await using var tablet = Connect(_kitchenToken);
    tablet.On<JsonElement>("StationOrdersChanged",
                           payload => heardByTheTablet.TrySetResult(payload.GetProperty("stationId").GetGuid()));

    await using var phone = Connect(_context.DeviceToken);
    phone.On<JsonElement>("StationOrdersChanged",
                          payload => heardByThePhone.TrySetResult(payload.GetProperty("stationId").GetGuid()));

    await tablet.StartAsync();
    await phone.StartAsync();

    using (var placed = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    var received = await Task.WhenAny(heardByTheTablet.Task, Task.Delay(_patience));

    Assert.That(received, Is.SameAs(heardByTheTablet.Task), "The tablet of the station must be told about its new slice.");

    Assert.Multiple(() =>
                    {
                      Assert.That(heardByTheTablet.Task.Result, Is.EqualTo(_context.World.KitchenStationId));
                      Assert.That(heardByThePhone.Task.IsCompleted,
                                  Is.False,
                                  "A waiter phone has no use for the queue of a station.");
                    });
  }

  [Test]
  public async Task AdvanceItems_AtThatStation_TellsItsOwnTablet()
  {
    TaskCompletionSource<Guid> heard = new(TaskCreationOptions.RunContinuationsAsynchronously);

    using (var placed = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    IReadOnlyList<Guid> orderItemIds = await KitchenItemIdsAsync();

    await using var tablet = Connect(_kitchenToken);
    tablet.On<JsonElement>("StationOrdersChanged",
                           payload => heard.TrySetResult(payload.GetProperty("stationId").GetGuid()));

    await tablet.StartAsync();

    using (var advanced = await _context.SendAsAsync(_kitchenToken,
                                                     HttpMethod.Post,
                                                     "/api/station/items/status",
                                                     new StationItemStatusBody(orderItemIds, "inProduction")))
    {
      Assert.That(advanced.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    var received = await Task.WhenAny(heard.Task, Task.Delay(_patience));

    Assert.That(received, Is.SameAs(heard.Task));
  }

  [Test]
  public async Task AdvanceItems_AtThatStation_TellsTheWaiterPhonesThatTheOrderMovedOn()
  {
    TaskCompletionSource<Guid> heard = new(TaskCreationOptions.RunContinuationsAsynchronously);

    Guid orderId;

    using (var placed = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));

      var body = JsonDocument.Parse(await placed.Content.ReadAsStringAsync());
      orderId = body.RootElement.GetProperty("orderId").GetGuid();
    }

    IReadOnlyList<Guid> orderItemIds = await KitchenItemIdsAsync();

    await using var phone = Connect(_context.DeviceToken);
    phone.On<JsonElement>("OrderStatusChanged",
                          payload => heard.TrySetResult(payload.GetProperty("orderId").GetGuid()));

    await phone.StartAsync();

    using (var advanced = await _context.SendAsAsync(_kitchenToken,
                                                     HttpMethod.Post,
                                                     "/api/station/items/status",
                                                     new StationItemStatusBody(orderItemIds, "inProduction")))
    {
      Assert.That(advanced.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    var received = await Task.WhenAny(heard.Task, Task.Delay(_patience));

    Assert.That(received,
                Is.SameAs(heard.Task),
                "The waiter who sent the order must learn that the station has started on it.");

    Assert.That(await heard.Task, Is.EqualTo(orderId));
  }

  private async Task<IReadOnlyList<Guid>> KitchenItemIdsAsync()
  {
    using var response = await _context.SendAsAsync(_kitchenToken, HttpMethod.Get, "/api/station/orders");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return
    [
      .. body.RootElement
             .GetProperty("slices")[0]
             .GetProperty("items")
             .EnumerateArray()
             .Select(item => item.GetProperty("orderItemId").GetGuid())
    ];
  }

  private HubConnection Connect(string deviceToken)
  {
    return new HubConnectionBuilder()
          .WithUrl(new Uri(_context.Factory.BaseAddress, $"hub?access_token={deviceToken}"))
          .Build();
  }
}
