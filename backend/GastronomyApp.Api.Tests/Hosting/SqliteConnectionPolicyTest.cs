using System.Data.Common;
using System.Globalization;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Hosting;

[TestFixture]
public sealed class SqliteConnectionPolicyTest
{
  private ApiTestFactory factory = null!;

  [SetUp]
  public async Task SetUp()
  {
    factory = await new ApiTestFactory.Builder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await factory.DisposeAsync();
  }

  [Test]
  public async Task ResolvedContext_ConnectionFromTheBuiltApplication_CarriesTheBusyTimeoutPolicy()
  {
    string busyTimeout = await ReadPragmaAsync("busy_timeout");

    Assert.That(
        busyTimeout,
        Is.EqualTo("5000"),
        "The composition root must apply the busy timeout the transaction runner sizes its BEGIN IMMEDIATE from.");
  }

  [Test]
  public async Task ResolvedContext_ConnectionFromTheBuiltApplication_CarriesTheWriteAheadLogPolicy()
  {
    string journalMode = await ReadPragmaAsync("journal_mode");

    Assert.That(journalMode, Is.EqualTo("wal").IgnoreCase);
  }

  private async Task<string> ReadPragmaAsync(string pragmaName)
  {
    await using GastronomyAppDbContext context = factory.CreateContext();
    await context.Database.OpenConnectionAsync();

    DbConnection connection = context.Database.GetDbConnection();
    await using DbCommand command = connection.CreateCommand();
    command.CommandText = $"PRAGMA {pragmaName};";
    object? value = await command.ExecuteScalarAsync();

    await context.Database.CloseConnectionAsync();

    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
  }
}
