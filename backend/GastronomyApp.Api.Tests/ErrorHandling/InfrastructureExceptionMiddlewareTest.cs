using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FakeItEasy;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Tests.TestSupport;
using GastronomyApp.Contracts;
using GastronomyApp.Infrastructure.Enums;
using GastronomyApp.Infrastructure.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Events;

namespace GastronomyApp.Api.Tests.ErrorHandling;

[TestFixture]
public sealed class InfrastructureExceptionMiddlewareTest
{
  [Test]
  public async Task InvokeAsync_DatabaseUnavailable_WritesTheServiceUnavailableEnvelope()
  {
    InfrastructureExceptionMiddleware middleware = new(BuildEnvelope(), NullLogger<InfrastructureExceptionMiddleware>.Instance);
    DefaultHttpContext context = new() { RequestServices = BuildRequestServices() };
    using MemoryStream body = new();
    context.Response.Body = body;

    await middleware.InvokeAsync(context, _ => throw new InfrastructureException(InfrastructureFailureReason.DatabaseUnavailable, "The database file could not be written."));

    body.Position = 0;
    var error = await JsonSerializer.DeserializeAsync<ApiError>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    Assert.Multiple(() =>
                    {
                      Assert.That(context.Response.StatusCode, Is.EqualTo(503));
                      Assert.That(error!.Code, Is.EqualTo("DatabaseUnavailable"));
                      Assert.That(error.MessageKey, Is.EqualTo("errors.storage.databaseUnavailable"));
                    });
  }

  [Test]
  public async Task InvokeAsync_DatabaseUnavailable_LogsTheExceptionWithItsSqliteReasonAtError()
  {
    var logger = A.Fake<ILogger<InfrastructureExceptionMiddleware>>();
    InfrastructureExceptionMiddleware middleware = new(BuildEnvelope(), logger);
    DefaultHttpContext context = new() { RequestServices = BuildRequestServices() };
    using MemoryStream body = new();
    context.Response.Body = body;
    InfrastructureException failure = new(InfrastructureFailureReason.DatabaseUnavailable, "The order database could not be written to.", new SqliteException("SQLite Error 5: 'database is locked'.", 5));

    await middleware.InvokeAsync(context, _ => throw failure);

    A.CallTo(logger).Where(call => call.Method.Name == nameof(ILogger.Log) && call.GetArgument<LogLevel>(0) == LogLevel.Error && call.GetArgument<Exception?>(3) == failure).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task InvokeAsync_ConflictingChange_WritesARetryableConflictEnvelope()
  {
    InfrastructureExceptionMiddleware middleware = new(BuildEnvelope(), NullLogger<InfrastructureExceptionMiddleware>.Instance);
    DefaultHttpContext context = new() { RequestServices = BuildRequestServices() };
    using MemoryStream body = new();
    context.Response.Body = body;

    await middleware.InvokeAsync(context, _ => throw new InfrastructureException(InfrastructureFailureReason.ConflictingChange, "Another write reached the same unique row first."));

    body.Position = 0;
    var error = await JsonSerializer.DeserializeAsync<ApiError>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    Assert.Multiple(() =>
                    {
                      Assert.That(context.Response.StatusCode, Is.EqualTo(409));
                      Assert.That(error!.Code, Is.EqualTo("ConflictingChange"));
                      Assert.That(error.MessageKey, Is.EqualTo("errors.storage.conflictingChange"));
                    });
  }

  [Test]
  public async Task InvokeAsync_AWriteWhileAnotherConnectionHoldsTheWriteLock_AnswersUnavailableAndLogsTheBusyDatabaseAtError()
  {
    using RecordedLog log = new();
    await using var factory = await new ApiTestFactoryBuilder().StartAsync();
    await using SqliteConnection lockHolder = new($"Data Source={Path.Combine(factory.DataDirectory, "gastronomy.db")}");
    await lockHolder.OpenAsync();
    await ExecuteAsync(lockHolder, "BEGIN IMMEDIATE;");

    try
    {
      using var response = await factory.Client.PostAsJsonAsync("/api/admin/enrolment/invitations", new { });
      List<LogEvent> errors = log.ReadEntries().Where(entry => entry.Level == LogEventLevel.Error).ToList();

      Assert.Multiple(() =>
                      {
                        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
                        Assert.That(errors.Select(entry => (entry.Exception?.InnerException as SqliteException)?.SqliteErrorCode), Has.Member(5));
                      });
    }
    finally
    {
      await ExecuteAsync(lockHolder, "ROLLBACK;");
    }
  }

  [Test]
  public void InvokeAsync_UnclassifiedException_RethrowsIt()
  {
    InfrastructureExceptionMiddleware middleware = new(BuildEnvelope(), NullLogger<InfrastructureExceptionMiddleware>.Instance);
    DefaultHttpContext context = new();

    Assert.That(async () => await middleware.InvokeAsync(context, _ => throw new InvalidOperationException("unclassified")), Throws.TypeOf<InvalidOperationException>());
  }

  private async Task ExecuteAsync(SqliteConnection connection, string statement)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = statement;
    await command.ExecuteNonQueryAsync();
  }

  private ServiceProvider BuildRequestServices()
  {
    ServiceCollection services = new();
    services.AddLogging();
    return services.BuildServiceProvider();
  }

  private ResultEnvelope BuildEnvelope()
  {
    return new(new(), new HttpContextAccessor(), NullLogger<ResultEnvelope>.Instance);
  }
}
