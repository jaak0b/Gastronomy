namespace GastronomyApp.Core.Ports;

public interface ITransactionRunner
{
  public Task<TValue> RunAsync<TValue>(Func<CancellationToken, Task<TransactionOutcome<TValue>>> body, CancellationToken cancellationToken);
}
