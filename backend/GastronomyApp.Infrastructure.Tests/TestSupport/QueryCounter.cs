using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class QueryCounter : DbCommandInterceptor
{
  public int QueryCount { get; private set; }

  public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
  {
    QueryCount++;

    return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
  }
}
