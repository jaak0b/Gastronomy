using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure;

public sealed record TransactionOutcome<TValue>
{
  public required TValue Value { get; init; }
  public required bool ShouldCommit { get; init; }
}

public sealed class ImmediateTransactionRunner
{
  private readonly SqliteFailureTranslator _failureTranslator = new();

  public async Task<TValue> RunAsync<TValue>(
      GastronomyAppDbContext dbContext,
      Func<CancellationToken, Task<TransactionOutcome<TValue>>> body,
      CancellationToken cancellationToken)
  {
    AutoTransactionBehavior previousBehavior = dbContext.Database.AutoTransactionBehavior;
    if (previousBehavior == AutoTransactionBehavior.Never)
    {
      throw new InvalidOperationException(
          "This context is already inside an immediate transaction, and SQLite cannot nest one inside another.");
    }

    await dbContext.Database.OpenConnectionAsync(cancellationToken);
    DbConnection connection = dbContext.Database.GetDbConnection();
    bool transactionIsOpen = false;

    try
    {
      dbContext.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;

      await ExecuteAsync(connection, "BEGIN IMMEDIATE", BusyTimeoutSecondsOf(connection), cancellationToken);
      transactionIsOpen = true;

      TransactionOutcome<TValue> outcome = await body(cancellationToken);

      await ExecuteAsync(connection, outcome.ShouldCommit ? "COMMIT" : "ROLLBACK", null, cancellationToken);
      transactionIsOpen = false;

      return outcome.Value;
    }
    catch (SqliteException exception) when (_failureTranslator.IsDatabaseUnavailable(exception))
    {
      throw _failureTranslator.Translate(exception);
    }
    catch (DbUpdateException exception)
        when (exception.InnerException is SqliteException inner && _failureTranslator.IsDatabaseUnavailable(inner))
    {
      throw _failureTranslator.Translate(inner);
    }
    catch (DbUpdateException exception)
        when (exception.InnerException is SqliteException inner
            && _failureTranslator.IsUniqueConstraintViolation(inner))
    {
      throw _failureTranslator.TranslateConflict(inner);
    }
    finally
    {
      if (transactionIsOpen)
      {
        await RollbackAbandonedTransactionAsync(connection);
      }

      dbContext.Database.AutoTransactionBehavior = previousBehavior;
      await dbContext.Database.CloseConnectionAsync();
    }
  }

  private async Task RollbackAbandonedTransactionAsync(DbConnection connection)
  {
    try
    {
      await ExecuteAsync(connection, "ROLLBACK", null, CancellationToken.None);
    }
    catch (SqliteException rollbackException)
    {
      throw new InfrastructureException(
          InfrastructureFailureReason.DatabaseUnavailable,
          "The transaction could not be rolled back after the operation failed.",
          rollbackException);
    }
  }

  private async Task ExecuteAsync(
      DbConnection connection,
      string statement,
      int? commandTimeoutSeconds,
      CancellationToken cancellationToken)
  {
    await using DbCommand command = connection.CreateCommand();
    command.CommandText = statement;
    if (commandTimeoutSeconds is not null)
    {
      command.CommandTimeout = commandTimeoutSeconds.Value;
    }

    await command.ExecuteNonQueryAsync(cancellationToken);
  }

  private int BusyTimeoutSecondsOf(DbConnection connection)
  {
    using DbCommand command = connection.CreateCommand();
    command.CommandText = "PRAGMA busy_timeout";
    object? value = command.ExecuteScalar();
    double milliseconds = value is null ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);

    return Math.Max(1, (int)Math.Ceiling(milliseconds / 1000d));
  }
}
