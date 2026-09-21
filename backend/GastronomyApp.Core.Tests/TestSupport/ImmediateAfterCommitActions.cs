using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Tests.TestSupport;

public sealed class ImmediateAfterCommitActions : IAfterCommitActions
{
  public Task RunWhenCommittedAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(action);

    return action(cancellationToken);
  }
}
