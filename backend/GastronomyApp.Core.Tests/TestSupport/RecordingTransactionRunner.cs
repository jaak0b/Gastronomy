using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Tests.TestSupport;

public sealed class RecordingTransactionRunner : ITransactionRunner
{
  public bool? Committed { get; private set; }

  public async Task<TValue> RunAsync<TValue>(Func<CancellationToken, Task<TransactionOutcome<TValue>>> body, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(body);

    TransactionOutcome<TValue> outcome = await body(cancellationToken);
    Committed = outcome.ShouldCommit;

    return outcome.Value;
  }
}
