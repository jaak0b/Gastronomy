using GastronomyApp.Core.Announcements;

namespace GastronomyApp.Core.Ports;

public interface IAfterCommitActions
{
  public Task RunWhenCommittedAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);

  public Task SendOnceWhenCommittedAsync(HubEvent hubEvent, CancellationToken cancellationToken);
}
