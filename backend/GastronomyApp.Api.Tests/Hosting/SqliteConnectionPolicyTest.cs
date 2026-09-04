using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Hosting;

[TestFixture]
public sealed class SqliteConnectionPolicyTest
{

  [SetUp]
  public async Task SetUp()
  {
    _factory = await new ApiTestFactory.Builder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _factory.DisposeAsync();
  }

  private ApiTestFactory _factory = null!;

  [Test]
  public async Task ResolvedContext_ConnectionFromTheBuiltApplication_CarriesTheBusyTimeoutPolicy()
  {
    var busyTimeout = await ReadPragmaAsync("busy_timeout");

    Assert.That(busyTimeout,
                Is.EqualTo("5000"),
                "The composition root must apply the busy timeout the transaction runner sizes its BEGIN IMMEDIATE from.");
  }

  [Test]
  public async Task ResolvedContext_ConnectionFromTheBuiltApplication_CarriesTheWriteAheadLogPolicy()
  {
    var journalMode = await ReadPragmaAsync("journal_mode");

    Assert.That(journalMode, Is.EqualTo("wal").IgnoreCase);
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
}
