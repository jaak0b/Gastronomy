using Microsoft.AspNetCore.SignalR.Client;

namespace GastronomyApp.Api.Tests.TestSupport;

public sealed class HubEventListener : IAsyncDisposable
{
  private readonly HubConnection _connection;
  private readonly TaskCompletionSource _firstHeard = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private readonly TaskCompletionSource _groupsJoined = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private readonly TimeSpan _groupsJoinedPatience = TimeSpan.FromSeconds(10);
  private int _heardCount;

  public HubEventListener(ApiTestFactory laptop, string hubPath, string eventName)
  {
    ArgumentNullException.ThrowIfNull(laptop);

    _connection = new HubConnectionBuilder().WithUrl(new Uri(laptop.BaseAddress, hubPath)).Build();
    _connection.On(eventName,
                   () =>
                   {
                     Interlocked.Increment(ref _heardCount);
                     _firstHeard.TrySetResult();
                   });
    _connection.On("GroupsJoined", () => _groupsJoined.TrySetResult());
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

    var joined = await Task.WhenAny(_groupsJoined.Task, Task.Delay(_groupsJoinedPatience));

    if (joined != _groupsJoined.Task)
      throw new TimeoutException($"The hub connection {_connection.ConnectionId} was not told within {_groupsJoinedPatience.TotalSeconds} seconds that it joined its groups.");
  }
}
