using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Tests.TestSupport;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Hub;

[TestFixture]
public sealed class GastronomyHubTest
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

  private readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

  private OrderTestContext _context = null!;

  [Test]
  public async Task OnConnectedAsync_APhoneConnects_TellsThePhoneOnceItJoinedItsGroupsSoTheNextBroadcastReachesIt()
  {
    await AssertTheConnectionHearsABroadcastSentRightAfterGroupsJoinedAsync($"hub?access_token={_context.DeviceToken}", Names.HubGroups.Devices);
  }

  [Test]
  public async Task OnConnectedAsync_AStationTabletConnects_TellsTheTabletOnceItJoinedItsGroupsSoTheNextBroadcastReachesIt()
  {
    var kitchenToken = await _context.IssueStationTokenAsync(_context.World.KitchenStationId);

    await AssertTheConnectionHearsABroadcastSentRightAfterGroupsJoinedAsync($"hub?access_token={kitchenToken}", Names.HubGroups.Stations);
  }

  [Test]
  public async Task OnConnectedAsync_TheLaptopConnectsWithoutADeviceToken_TellsTheLaptopOnceItJoinedTheAdminGroupSoTheNextBroadcastReachesIt()
  {
    await AssertTheConnectionHearsABroadcastSentRightAfterGroupsJoinedAsync("hub", Names.HubGroups.Admin);
  }

  private async Task AssertTheConnectionHearsABroadcastSentRightAfterGroupsJoinedAsync(string relativeHubUrl, string group)
  {
    TaskCompletionSource groupsJoined = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource broadcastHeard = new(TaskCreationOptions.RunContinuationsAsynchronously);
    var groupsJoinedCount = 0;

    await using var connection = new HubConnectionBuilder().WithUrl(new Uri(_context.Factory.BaseAddress, relativeHubUrl)).Build();
    connection.On("GroupsJoined",
                  () =>
                  {
                    Interlocked.Increment(ref groupsJoinedCount);
                    groupsJoined.TrySetResult();
                  });
    connection.On("OrdersChanged", () => broadcastHeard.TrySetResult());

    await connection.StartAsync();

    var joined = await Task.WhenAny(groupsJoined.Task, Task.Delay(_patience));

    Assert.That(joined, Is.SameAs(groupsJoined.Task), "The hub must tell a new connection once it has joined every group.");

    await _context.Factory.Services.GetRequiredService<IHubContext<GastronomyHub>>().Clients.Group(group).SendAsync("OrdersChanged");

    var heard = await Task.WhenAny(broadcastHeard.Task, Task.Delay(_patience));

    Assert.Multiple(() =>
                    {
                      Assert.That(heard, Is.SameAs(broadcastHeard.Task), "A broadcast sent right after GroupsJoined must reach the connection.");
                      Assert.That(Volatile.Read(ref groupsJoinedCount), Is.EqualTo(1), "A connection is told exactly once that it joined its groups.");
                    });
  }
}
