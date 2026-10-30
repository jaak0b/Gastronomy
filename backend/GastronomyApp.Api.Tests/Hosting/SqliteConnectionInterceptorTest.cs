using FakeItEasy;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Tests.Hosting;

[TestFixture]
public sealed class SqliteConnectionInterceptorTest
{
  [SetUp]
  public void SetUp()
  {
    _logger = A.Fake<ILogger<SqliteConnectionInterceptor>>();
    _interceptor = new(new(), _logger);
    _filesThatAreNotADatabase = [];
  }

  [TearDown]
  public void TearDown()
  {
    SqliteConnection.ClearAllPools();

    foreach (var path in _filesThatAreNotADatabase)
      File.Delete(path);
  }

  private List<string> _filesThatAreNotADatabase = null!;
  private SqliteConnectionInterceptor _interceptor = null!;
  private ILogger<SqliteConnectionInterceptor> _logger = null!;

  [Test]
  public async Task ConnectionFailedAsync_AnOpenThatCannotReachTheFile_LogsAnErrorWithTheExceptionAndItsReason()
  {
    await using var context = ContextOn(new SqliteConnection($"Data Source={AFileInAFolderThatDoesNotExist()}"));

    var failure = Assert.ThrowsAsync<SqliteException>(async () => await context.Database.OpenConnectionAsync());

    A.CallTo(_logger).Where(call => IsLogAt(call, LogLevel.Error) && call.GetArgument<Exception?>(3) == failure && Rendered(call).Contains(failure!.Message, StringComparison.Ordinal)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task ConnectionFailedAsync_AnOpenCancelledByItsOwnToken_LogsAtDebugAndNothingAtError()
  {
    await using var context = ContextOn(new SqliteConnection("Data Source=:memory:"));
    using CancellationTokenSource cancellation = new();
    await cancellation.CancelAsync();

    Assert.That(async () => await context.Database.OpenConnectionAsync(cancellation.Token), Throws.InstanceOf<OperationCanceledException>());

    A.CallTo(_logger).Where(call => IsLogAt(call, LogLevel.Debug) && call.GetArgument<Exception?>(3) is OperationCanceledException).MustHaveHappenedOnceExactly();
    A.CallTo(_logger).Where(call => IsLogAt(call, LogLevel.Error)).MustNotHaveHappened();
  }

  [Test]
  public async Task ConnectionFailedAsync_AnOpenAbandonedWhileItsTokenWasNotCancelled_LogsAnError()
  {
    await using var context = ContextOn(new ConnectionWhoseOpenIsAbandoned());

    Assert.That(async () => await context.Database.OpenConnectionAsync(CancellationToken.None), Throws.InstanceOf<OperationCanceledException>());

    A.CallTo(_logger).Where(call => IsLogAt(call, LogLevel.Error) && call.GetArgument<Exception?>(3) is OperationCanceledException).MustHaveHappenedOnceExactly();
    A.CallTo(_logger).Where(call => IsLogAt(call, LogLevel.Debug)).MustNotHaveHappened();
  }

  [Test]
  public void ConnectionFailed_ASynchronousOpenThatCannotReachTheFile_LogsAnErrorWithTheException()
  {
    using var context = ContextOn(new SqliteConnection($"Data Source={AFileInAFolderThatDoesNotExist()}"));

    var failure = Assert.Throws<SqliteException>(() => context.Database.OpenConnection());

    A.CallTo(_logger).Where(call => IsLogAt(call, LogLevel.Error) && call.GetArgument<Exception?>(3) == failure).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task ConnectionFailedAsync_ACloseThatFails_LogsAnErrorWithTheException()
  {
    await using var context = ContextOn(new ConnectionWhoseFirstCloseFails());
    await context.Database.OpenConnectionAsync();

    var failure = Assert.ThrowsAsync<InvalidOperationException>(async () => await context.Database.CloseConnectionAsync());

    A.CallTo(_logger).Where(call => IsLogAt(call, LogLevel.Error) && call.GetArgument<Exception?>(3) == failure).MustHaveHappenedOnceExactly();
  }

  [Test]
  public void ConnectionFailed_ASynchronousCloseThatFails_LogsAnErrorWithTheException()
  {
    using var context = ContextOn(new ConnectionWhoseFirstCloseFails());
    context.Database.OpenConnection();

    var failure = Assert.Throws<InvalidOperationException>(() => context.Database.CloseConnection());

    A.CallTo(_logger).Where(call => IsLogAt(call, LogLevel.Error) && call.GetArgument<Exception?>(3) == failure).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task ConnectionFailedAsync_ThePolicyStatementsOnAFileThatIsNotADatabase_LogOneErrorWithTheException()
  {
    var path = AFileThatIsNotADatabase();
    await using var contextWithoutThePolicy = ContextWithoutTheInterceptorOn(new SqliteConnection($"Data Source={path}"));
    await using var context = ContextOn(new SqliteConnection($"Data Source={path}"));

    Assert.That(async () => await contextWithoutThePolicy.Database.OpenConnectionAsync(), Throws.Nothing);
    var failure = Assert.ThrowsAsync<SqliteException>(async () => await context.Database.OpenConnectionAsync());

    Assert.That(failure!.SqliteErrorCode, Is.EqualTo(26));
    A.CallTo(_logger).Where(call => IsLogAt(call, LogLevel.Error)).MustHaveHappenedOnceExactly();
    A.CallTo(_logger).Where(call => IsLogAt(call, LogLevel.Error) && call.GetArgument<Exception?>(3) == failure).MustHaveHappenedOnceExactly();
  }

  [Test]
  public void ConnectionFailed_ThePolicyStatementsOfASynchronousOpenOnAFileThatIsNotADatabase_LogOneErrorWithTheException()
  {
    var path = AFileThatIsNotADatabase();
    using var contextWithoutThePolicy = ContextWithoutTheInterceptorOn(new SqliteConnection($"Data Source={path}"));
    using var context = ContextOn(new SqliteConnection($"Data Source={path}"));

    Assert.That(() => contextWithoutThePolicy.Database.OpenConnection(), Throws.Nothing);
    var failure = Assert.Throws<SqliteException>(() => context.Database.OpenConnection());

    Assert.That(failure!.SqliteErrorCode, Is.EqualTo(26));
    A.CallTo(_logger).Where(call => IsLogAt(call, LogLevel.Error)).MustHaveHappenedOnceExactly();
    A.CallTo(_logger).Where(call => IsLogAt(call, LogLevel.Error) && call.GetArgument<Exception?>(3) == failure).MustHaveHappenedOnceExactly();
  }

  private GastronomyAppDbContext ContextOn(SqliteConnection connection)
  {
    return new(new DbContextOptionsBuilder<GastronomyAppDbContext>().UseSqlite(connection, contextOwnsConnection: true).AddInterceptors(_interceptor).Options);
  }

  private GastronomyAppDbContext ContextWithoutTheInterceptorOn(SqliteConnection connection)
  {
    return new(new DbContextOptionsBuilder<GastronomyAppDbContext>().UseSqlite(connection, contextOwnsConnection: true).Options);
  }

  private string AFileInAFolderThatDoesNotExist()
  {
    return Path.Combine(Path.GetTempPath(), $"gastronomy-missing-{Guid.NewGuid():N}", "gastronomy.db");
  }

  private string AFileThatIsNotADatabase()
  {
    var path = Path.Combine(Path.GetTempPath(), $"gastronomy-not-a-database-{Guid.NewGuid():N}.db");
    File.WriteAllBytes(path, Enumerable.Range(0, 4096).Select(index => (byte)(index % 251)).ToArray());
    _filesThatAreNotADatabase.Add(path);

    return path;
  }

  private bool IsLogAt(FakeItEasy.Core.IFakeObjectCall call, LogLevel level)
  {
    return call.Method.Name == nameof(ILogger.Log) && call.GetArgument<LogLevel>(0) == level;
  }

  private string Rendered(FakeItEasy.Core.IFakeObjectCall call)
  {
    return call.GetArgument<object>(2)?.ToString() ?? string.Empty;
  }

  private sealed class ConnectionWhoseOpenIsAbandoned : SqliteConnection
  {
    public ConnectionWhoseOpenIsAbandoned() : base("Data Source=:memory:")
    {
    }

    override public Task OpenAsync(CancellationToken cancellationToken)
    {
      return Task.FromException(new OperationCanceledException("The open was abandoned although nobody cancelled it."));
    }
  }

  private sealed class ConnectionWhoseFirstCloseFails : SqliteConnection
  {
    private bool _closeHasFailed;

    public ConnectionWhoseFirstCloseFails() : base("Data Source=:memory:")
    {
    }

    override public void Close()
    {
      if (_closeHasFailed)
      {
        base.Close();

        return;
      }

      _closeHasFailed = true;

      throw new InvalidOperationException("The close failed.");
    }

    override public Task CloseAsync()
    {
      if (_closeHasFailed)
        return base.CloseAsync();

      _closeHasFailed = true;

      return Task.FromException(new InvalidOperationException("The close failed."));
    }
  }
}
