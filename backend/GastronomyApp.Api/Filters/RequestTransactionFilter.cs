using System.Data.Common;
using System.Globalization;
using ErrorOr;
using GastronomyApp.Infrastructure.Enums;
using GastronomyApp.Infrastructure.ErrorHandling;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Filters;

public sealed class RequestTransactionFilter : IEndpointFilter
{
  private const int AttemptsBeforeGivingUp = 5;

  private readonly SqliteFailureTranslator _failureTranslator;
  private readonly ILogger<RequestTransactionFilter> _logger;

  public RequestTransactionFilter(SqliteFailureTranslator failureTranslator, ILogger<RequestTransactionFilter> logger)
  {
    _failureTranslator = failureTranslator;
    _logger = logger;
  }

  public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(next);

    if (!ChangesStoredData(context.HttpContext.Request.Method))
      return await next(context);

    var database = context.HttpContext.RequestServices.GetRequiredService<GastronomyAppDbContext>();
    var afterCommitActions = context.HttpContext.RequestServices.GetRequiredService<AfterCommitActions>();
    DbUpdateConcurrencyException? lostRace = null;

    for (var attempt = 1; attempt <= AttemptsBeforeGivingUp; attempt++)
      try
      {
        return await RunOnceAsync(context, next, database, afterCommitActions);
      }
      catch (DbUpdateConcurrencyException exception)
      {
        lostRace = exception;
        database.ChangeTracker.Clear();

        if (attempt < AttemptsBeforeGivingUp)
          _logger.LogWarning("Attempt {Attempt} of {AttemptsBeforeGivingUp} to answer {RequestPath} inside an immediate transaction lost a row to another writer, so it is being tried again.", attempt, AttemptsBeforeGivingUp, context.HttpContext.Request.Path.Value);
      }

    throw new InfrastructureException(InfrastructureFailureReason.ConflictingChange, "Another writer took the rows this request had read, and every attempt to write them lost that race.", lostRace!);
  }

  private async Task<object?> RunOnceAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next, GastronomyAppDbContext database, AfterCommitActions afterCommitActions)
  {
    var cancellationToken = context.HttpContext.RequestAborted;
    var previousBehavior = database.Database.AutoTransactionBehavior;

    if (previousBehavior == AutoTransactionBehavior.Never)
      throw new InvalidOperationException("This request is already inside an immediate transaction, and SQLite cannot nest one inside another.");

    await database.Database.OpenConnectionAsync(cancellationToken);
    var connection = database.Database.GetDbConnection();
    var transactionIsOpen = false;
    IReadOnlyList<Func<CancellationToken, Task>> committedActions = [];
    object? answer;

    afterCommitActions.StartCollecting();

    try
    {
      database.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;

      await ExecuteAsync(connection, "BEGIN IMMEDIATE", ReadBusyTimeoutSeconds(connection), cancellationToken);
      transactionIsOpen = true;

      answer = await next(context);

      var shouldCommit = IsSuccess(answer);

      await ExecuteAsync(connection, ClosingStatementFor(shouldCommit), null, cancellationToken);
      transactionIsOpen = false;

      if (shouldCommit)
        committedActions = afterCommitActions.TakeCollectedActions();
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
    }
    finally
    {
      afterCommitActions.DiscardCollectedActions();

      if (transactionIsOpen)
        await RollbackAbandonedTransactionAsync(connection);

      database.Database.AutoTransactionBehavior = previousBehavior;
      await database.Database.CloseConnectionAsync();
    }

    await RunCommittedActionsAsync(committedActions, cancellationToken);

    return answer;
  }

  private bool ChangesStoredData(string method)
  {
    return HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
  }

  private bool IsSuccess(object? answer)
  {
    if (answer is IErrorOr refusable)
      return !refusable.IsError;

    return true;
  }

  private async Task RunCommittedActionsAsync(IReadOnlyList<Func<CancellationToken, Task>> committedActions, CancellationToken cancellationToken)
  {
    foreach (Func<CancellationToken, Task> committedAction in committedActions)
      try
      {
        await committedAction(cancellationToken);
      }
      catch (Exception exception)
      {
        _logger.LogError(exception, "The change was committed, but the step waiting for that commit failed, so the phones and station tablets were not told about it and will load it the next time they connect.");
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
      throw new InfrastructureException(InfrastructureFailureReason.DatabaseUnavailable, "The transaction could not be rolled back after the request failed.", rollbackException);
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
