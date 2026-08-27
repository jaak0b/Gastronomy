using Microsoft.Data.Sqlite;

namespace GastronomyApp.Infrastructure;

public sealed class SqliteConnectionFactory
{
    public SqliteConnection Open(string dataSource)
    {
        SqliteConnection connection = new($"Data Source={dataSource}");
        connection.Open();

        using SqliteCommand pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 5000;";
        pragma.ExecuteNonQuery();

        return connection;
    }
}
