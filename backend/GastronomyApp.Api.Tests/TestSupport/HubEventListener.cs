using GastronomyApp.Api.Hub;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.TestSupport;

public sealed class HubEventListener : IAsyncDisposable
{
  private const string JoinProbeEvent = "ListenerJoinProbe";

  private readonly HubConnection _connection;
  private readonly TaskCompletionSource _firstHeard = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private readonly IHubContext<GastronomyHub> _laptopHub;
  private readonly TimeSpan _joinPatience = TimeSpan.FromSeconds(10);
  private readonly TimeSpan _probeInterval = TimeSpan.FromMilliseconds(50);
  private readonly TaskCompletionSource _joinedItsGroups = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private int _heardCount;

  public HubEventListener(ApiTestFactory laptop, string hubPath, string eventName)
  {
    ArgumentNullException.ThrowIfNull(laptop);

    _laptopHub = laptop.Services.GetRequiredService<IHubContext<GastronomyHub>>();
    _connection = new HubConnectionBuilder().WithUrl(new Uri(laptop.BaseAddress, hubPath)).Build();
    _connection.On(eventName,
                   () =>
                   {
                     Interlocked.Increment(ref _heardCount);
                     _firstHeard.TrySetResult();
                   });
    _connection.On(JoinProbeEvent, () => _joinedItsGroups.TrySetResult());
  }

  public int HeardCount => Volatile.Read(ref _heardCount);

  public Task FirstHeard => _firstHeard.Task;

  public ValueTask DisposeAsync()
  {
    return _connection.DisposeAsync();
  }

  public async Task StartAsync()
  {
    await _connection.StartAsync();

    var deadline = DateTime.UtcNow + _joinPatience;

    while (!_joinedItsGroups.Task.IsCompleted)
    {
      if (DateTime.UtcNow > deadline)
        throw new TimeoutException($"The laptop did not put the hub connection {_connection.ConnectionId} into any of its broadcast groups within {_joinPatience}.");

      await _laptopHub.Clients.Groups(Names.HubGroups.Devices, Names.HubGroups.Admin, Names.HubGroups.Stations).SendAsync(JoinProbeEvent);
      await Task.WhenAny(_joinedItsGroups.Task, Task.Delay(_probeInterval));
    }
  }
}
