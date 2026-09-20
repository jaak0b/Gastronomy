using System.Data.Common;
using System.Globalization;
using GastronomyApp.Core.Exceptions;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Infrastructure.Enums;
using GastronomyApp.Infrastructure.ErrorHandling;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Infrastructure.Persistence;

public sealed class ImmediateTransactionRunner : ITransactionRunner
{
  private const int AttemptsBeforeGivingUp = 5;

  private readonly GastronomyAppDbContext _dbContext;
  private readonly SqliteFailureTranslator _failureTranslator;
  private readonly ILogger<ImmediateTransactionRunner> _logger;

  public ImmediateTransactionRunner(GastronomyAppDbContext dbContext, SqliteFailureTranslator failureTranslator, ILogger<ImmediateTransactionRunner> logger)
  {
    _dbContext = dbContext;
    _failureTranslator = failureTranslator;
    _logger = logger;
  }

  public async Task<TValue> RunAsync<TValue>(Func<CancellationToken, Task<TransactionOutcome<TValue>>> body, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(body);

    DbUpdateConcurrencyException? lostRace = null;

    for (var attempt = 1; attempt <= AttemptsBeforeGivingUp; attempt++)
    {
      try
      {
        return await RunOnceAsync(body, cancellationToken);
      }
      catch (DbUpdateConcurrencyException exception)
      {
        lostRace = exception;
        _dbContext.ChangeTracker.Clear();

        if (attempt < AttemptsBeforeGivingUp)
          _logger.LogWarning("Attempt {Attempt} of {AttemptsBeforeGivingUp} to write inside an immediate transaction lost a row to another writer, so it is being tried again.", attempt, AttemptsBeforeGivingUp);
      }
    }

    throw new ConcurrentWriteException("Another writer took the rows this transaction had read, and every attempt to write them lost that race.", lostRace!);
  }

  private async Task<TValue> RunOnceAsync<TValue>(Func<CancellationToken, Task<TransactionOutcome<TValue>>> body, CancellationToken cancellationToken)
  {
    var previousBehavior = _dbContext.Database.AutoTransactionBehavior;
    if (previousBehavior == AutoTransactionBehavior.Never)
      throw new InvalidOperationException("This context is already inside an immediate transaction, and SQLite cannot nest one inside another.");

    await _dbContext.Database.OpenConnectionAsync(cancellationToken);
    var connection = _dbContext.Database.GetDbConnection();
    var transactionIsOpen = false;

    try
    {
      _dbContext.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;

      await ExecuteAsync(connection, "BEGIN IMMEDIATE", ReadBusyTimeoutSeconds(connection), cancellationToken);
      transactionIsOpen = true;

      TransactionOutcome<TValue> outcome = await body(cancellationToken);

      await ExecuteAsync(connection, ClosingStatementFor(outcome.ShouldCommit), null, cancellationToken);
      transactionIsOpen = false;

      return outcome.Value;
    }
    catch (SqliteException exception) when (_failureTranslator.IsDatabaseUnavailable(exception))
    {
      throw _failureTranslator.Translate(exception);
    }
    catch (DbUpdateException exception) when (exception.InnerException is SqliteException inner && _failureTranslator.IsDatabaseUnavailable(inner))
    {
      throw _failureTranslator.Translate(inner);
    }
    catch (DbUpdateException exception) when (exception.InnerException is SqliteException inner && _failureTranslator.IsUniqueConstraintViolation(inner))
    {
      throw _failureTranslator.TranslateConflict(inner);
    } finally
    {
      if (transactionIsOpen)
        await RollbackAbandonedTransactionAsync(connection);

      _dbContext.Database.AutoTransactionBehavior = previousBehavior;
      await _dbContext.Database.CloseConnectionAsync();
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
      throw new InfrastructureException(InfrastructureFailureReason.DatabaseUnavailable, "The transaction could not be rolled back after the operation failed.", rollbackException);
    }
  }

  private async Task ExecuteAsync(DbConnection connection, string statement, int? commandTimeoutSeconds, CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = statement;
    if (commandTimeoutSeconds is not null)
      command.CommandTimeout = commandTimeoutSeconds.Value;

    await command.ExecuteNonQueryAsync(cancellationToken);
  }

  private string ClosingStatementFor(bool shouldCommit)
  {
    if (shouldCommit)
      return "COMMIT";

    return "ROLLBACK";
  }

  private int ReadBusyTimeoutSeconds(DbConnection connection)
  {
    using var command = connection.CreateCommand();
    command.CommandText = "PRAGMA busy_timeout";
    var value = command.ExecuteScalar();
    var milliseconds = 0d;

    if (value is not null)
      milliseconds = Convert.ToDouble(value, CultureInfo.InvariantCulture);

    return Math.Max(1, (int)Math.Ceiling(milliseconds / 1000d));
  }
}
