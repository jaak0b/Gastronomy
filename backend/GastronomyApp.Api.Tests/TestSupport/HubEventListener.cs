using Microsoft.AspNetCore.SignalR.Client;

namespace GastronomyApp.Api.Tests.TestSupport;

public sealed class HubEventListener : IAsyncDisposable
{
  private readonly HubConnection _connection;
  private readonly TaskCompletionSource _firstHeard = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private int _heardCount;

  public HubEventListener(Uri hubAddress, string eventName)
  {
    _connection = new HubConnectionBuilder().WithUrl(hubAddress).Build();
    _connection.On(eventName,
                   () =>
                   {
                     Interlocked.Increment(ref _heardCount);
                     _firstHeard.TrySetResult();
                   });
  }

  public int HeardCount => Volatile.Read(ref _heardCount);

  public Task FirstHeard => _firstHeard.Task;

  public ValueTask DisposeAsync()
  {
    return _connection.DisposeAsync();
  }

  public Task StartAsync()
  {
    return _connection.StartAsync();
  }
}
