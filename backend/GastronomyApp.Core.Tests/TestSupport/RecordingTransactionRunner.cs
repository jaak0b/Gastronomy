using ErrorOr;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Tests.TestSupport;

public sealed class RecordingTransactionRunner : ITransactionRunner
{
  public bool? Committed { get; private set; }

  public async Task<ErrorOr<TValue>> RunAsync<TValue>(Func<CancellationToken, Task<ErrorOr<TValue>>> body, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(body);

    ErrorOr<TValue> outcome = await body(cancellationToken);
    Committed = !outcome.IsError;

    return outcome;
  }
}
