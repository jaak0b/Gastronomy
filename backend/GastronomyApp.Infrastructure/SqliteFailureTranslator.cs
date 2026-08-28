using Microsoft.Data.Sqlite;

namespace GastronomyApp.Infrastructure;

public sealed class SqliteFailureTranslator
{
  private const int SqliteBusy = 5;
  private const int SqliteLocked = 6;
  private const int SqliteReadOnly = 8;
  private const int SqliteInputOutputError = 10;
  private const int SqliteCorrupt = 11;
  private const int SqliteCannotOpen = 14;
  private const int SqliteConstraint = 19;

  public bool IsDatabaseUnavailable(SqliteException exception)
  {
    return exception.SqliteErrorCode switch
    {
      SqliteBusy => true,
      SqliteLocked => true,
      SqliteReadOnly => true,
      SqliteInputOutputError => true,
      SqliteCorrupt => true,
      SqliteCannotOpen => true,
      _ => false,
    };
  }

  public bool IsUniqueConstraintViolation(SqliteException exception)
  {
    return exception.SqliteErrorCode == SqliteConstraint
        && exception.Message.Contains("UNIQUE constraint failed", StringComparison.Ordinal);
  }

  public InfrastructureException TranslateConflict(SqliteException exception)
  {
    return new InfrastructureException(
        InfrastructureFailureReason.ConflictingChange,
        "Another write reached the same unique row first.",
        exception);
  }

  public InfrastructureException Translate(SqliteException exception)
  {
    return new InfrastructureException(
        InfrastructureFailureReason.DatabaseUnavailable,
        "The order database could not be written to.",
        exception);
  }
}
