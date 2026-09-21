using ErrorOr;

namespace GastronomyApp.Core.Ports;

public interface ITransactionRunner
{
  public Task<ErrorOr<TValue>> RunAsync<TValue>(Func<CancellationToken, Task<ErrorOr<TValue>>> body, CancellationToken cancellationToken);
}
