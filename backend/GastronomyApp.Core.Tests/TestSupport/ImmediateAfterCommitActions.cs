using GastronomyApp.Core.Announcements;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Tests.TestSupport;

public sealed class ImmediateAfterCommitActions : IAfterCommitActions
{
  private readonly ICommittedChangeAnnouncer _announcer;

  public ImmediateAfterCommitActions(ICommittedChangeAnnouncer announcer)
  {
    _announcer = announcer;
  }

  public Task RunWhenCommittedAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(action);

    return action(cancellationToken);
  }

  public Task SendOnceWhenCommittedAsync(HubEvent hubEvent, CancellationToken cancellationToken)
  {
    return _announcer.AnnounceAsync(hubEvent, cancellationToken);
  }
}
