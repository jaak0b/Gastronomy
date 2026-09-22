using ErrorOr;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Persistence;

namespace GastronomyApp.Api.Tests.TestSupport;

public sealed class TransactionProbe
{
  public Func<GastronomyAppDbContext, IAfterCommitActions, CancellationToken, Task<ErrorOr<Guid>>> Handle { get; set; } = (_, _, _) => Task.FromResult<ErrorOr<Guid>>(Guid.Empty);

  public Exception? Failure { get; set; }
}
