using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace GastronomyApp.Infrastructure;

public sealed class SqliteConnectionFactory
{
    private const string ConnectionPolicyStatements = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 5000;";

    public SqliteConnection Open(string dataSource)
    {
        SqliteConnection connection = new($"Data Source={dataSource}");
        connection.Open();
        ApplyConnectionPolicy(connection);

        return connection;
    }

    public void ApplyConnectionPolicy(DbConnection connection)
    {
        using DbCommand command = connection.CreateCommand();
        command.CommandText = ConnectionPolicyStatements;
        command.ExecuteNonQuery();
    }

    public async Task ApplyConnectionPolicyAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = ConnectionPolicyStatements;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
