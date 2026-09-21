using GastronomyApp.Core.Ports;

namespace GastronomyApp.Infrastructure.Persistence;

public sealed class AfterCommitActions : IAfterCommitActions
{
  private readonly List<Func<CancellationToken, Task>> _collected = [];
  private bool _isCollecting;

  public Task RunWhenCommittedAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(action);

    if (!_isCollecting)
      return action(cancellationToken);

    _collected.Add(action);

    return Task.CompletedTask;
  }

  public void StartCollecting()
  {
    _isCollecting = true;
    _collected.Clear();
  }

  public IReadOnlyList<Func<CancellationToken, Task>> TakeCollectedActions()
  {
    Func<CancellationToken, Task>[] taken = _collected.ToArray();
    _isCollecting = false;
    _collected.Clear();

    return taken;
  }

  public void DiscardCollectedActions()
  {
    _isCollecting = false;
    _collected.Clear();
  }
}
