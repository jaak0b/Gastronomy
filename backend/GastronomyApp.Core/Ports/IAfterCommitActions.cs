namespace GastronomyApp.Core.Ports;

public interface IAfterCommitActions
{
  public Task RunWhenCommittedAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);
}
