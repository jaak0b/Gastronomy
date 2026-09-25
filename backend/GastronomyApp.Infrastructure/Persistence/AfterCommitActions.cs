using GastronomyApp.Core.Announcements;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Infrastructure.Persistence;

public sealed class AfterCommitActions : IAfterCommitActions
{
  private readonly List<Func<CancellationToken, Task>> _collected = [];
  private readonly HashSet<HubEvent> _enqueuedHubEvents = [];
  private readonly ICommittedChangeAnnouncer _announcer;
  private bool _isCollecting;

  public AfterCommitActions(ICommittedChangeAnnouncer announcer)
  {
    _announcer = announcer;
  }

  public Task RunWhenCommittedAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(action);

    if (!_isCollecting)
      return action(cancellationToken);

    _collected.Add(action);

    return Task.CompletedTask;
  }

  public Task SendOnceWhenCommittedAsync(HubEvent hubEvent, CancellationToken cancellationToken)
  {
    if (!_isCollecting)
      throw new InvalidOperationException($"A save raised {hubEvent} outside a request transaction, so there is no commit to send it after.");

    if (_enqueuedHubEvents.Add(hubEvent))
      _collected.Add(announcementCancellationToken => _announcer.AnnounceAsync(hubEvent, announcementCancellationToken));

    return Task.CompletedTask;
  }

  public void StartCollecting()
  {
    _isCollecting = true;
    _collected.Clear();
    _enqueuedHubEvents.Clear();
  }

  public IReadOnlyList<Func<CancellationToken, Task>> TakeCollectedActions()
  {
    Func<CancellationToken, Task>[] taken = _collected.ToArray();
    _isCollecting = false;
    _collected.Clear();
    _enqueuedHubEvents.Clear();

    return taken;
  }

  public void DiscardCollectedActions()
  {
    _isCollecting = false;
    _collected.Clear();
    _enqueuedHubEvents.Clear();
  }
}
