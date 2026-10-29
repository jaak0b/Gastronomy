using System.Globalization;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Api.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;

namespace GastronomyApp.Api.Tests.Hosting;

[TestFixture]
public sealed class SqliteConnectionPolicyTest
{
  [SetUp]
  public async Task SetUp()
  {
    _log = new();
    _factory = await new ApiTestFactoryBuilder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _factory.DisposeAsync();
    _log.Dispose();
  }

  private ApiTestFactory _factory = null!;
  private RecordedLog _log = null!;

  [Test]
  public async Task ResolvedContext_ConnectionFromTheBuiltApplication_CarriesTheBusyTimeoutPolicy()
  {
    var busyTimeout = await ReadPragmaAsync("busy_timeout");

    Assert.That(busyTimeout, Is.EqualTo("5000"), "The composition root must apply the busy timeout the transaction runner sizes its BEGIN IMMEDIATE from.");
  }

  [Test]
  public async Task ResolvedContext_ConnectionFromTheBuiltApplication_CarriesTheWriteAheadLogPolicy()
  {
    var journalMode = await ReadPragmaAsync("journal_mode");

    Assert.That(journalMode, Is.EqualTo("wal").IgnoreCase);
  }

  [Test]
  public async Task ResolvedContext_TheDatabaseFileCannotBeOpened_WritesOneErrorWithTheExceptionAndItsReason()
  {
    ReplaceTheDatabaseFileWithAFolder();
    await using var context = _factory.CreateContext();
    var entriesBeforeTheOpen = _log.ReadEntries().Count;

    var failure = Assert.ThrowsAsync<SqliteException>(async () => await context.Database.OpenConnectionAsync());
    List<LogEvent> errors = _log.ReadEntries().Skip(entriesBeforeTheOpen).Where(entry => entry.Level >= LogEventLevel.Error).ToList();

    Assert.Multiple(() =>
                    {
                      Assert.That(errors.Select(entry => entry.Exception), Is.EqualTo(new Exception?[] { failure }));
                      Assert.That(errors.Select(entry => entry.RenderMessage()), Has.All.Contain(failure!.Message));
                    });
  }

  [Test]
  public async Task ResolvedContext_AnOpenTheCallerCancelled_WritesItAtDebugAndNothingAtError()
  {
    await using var context = _factory.CreateContext();
    using CancellationTokenSource cancellation = new();
    await cancellation.CancelAsync();
    var entriesBeforeTheOpen = _log.ReadEntries().Count;

    Assert.That(async () => await context.Database.OpenConnectionAsync(cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
    List<LogEvent> entries = _log.ReadEntries().Skip(entriesBeforeTheOpen).ToList();

    Assert.Multiple(() =>
                    {
                      Assert.That(entries.Where(entry => entry.Level >= LogEventLevel.Error).Select(entry => entry.RenderMessage()), Is.Empty);
                      Assert.That(entries.Where(entry => entry.Level == LogEventLevel.Debug && entry.Exception is OperationCanceledException).Select(SourceOf), Has.Member(typeof(SqliteConnectionInterceptor).FullName));
                    });
  }

  private async Task<string> ReadPragmaAsync(string pragmaName)
  {
    await using var context = _factory.CreateContext();
    await context.Database.OpenConnectionAsync();

    var connection = context.Database.GetDbConnection();
    await using var command = connection.CreateCommand();
    command.CommandText = $"PRAGMA {pragmaName};";
    var value = await command.ExecuteScalarAsync();

    await context.Database.CloseConnectionAsync();

    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
  }

  private void ReplaceTheDatabaseFileWithAFolder()
  {
    var databasePath = Path.Combine(_factory.DataDirectory, "gastronomy.db");
    SqliteConnection.ClearAllPools();

    foreach (var path in new[]
                         {
                           databasePath,
                           $"{databasePath}-wal",
                           $"{databasePath}-shm"
                         })
      File.Delete(path);

    Directory.CreateDirectory(databasePath);
  }

  private string? SourceOf(LogEvent entry)
  {
    if (entry.Properties.TryGetValue("SourceContext", out var source) && source is ScalarValue { Value: string sourceName })
      return sourceName;

    return null;
  }
}
