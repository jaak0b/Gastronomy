using GastronomyApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.TestSupport;

public sealed class TransactionProbeHost : IAsyncDisposable
{
  private readonly WebApplication _application;

  internal TransactionProbeHost(WebApplication application, TransactionProbe probe, string databasePath, Uri baseAddress)
  {
    _application = application;
    DatabasePath = databasePath;
    Probe = probe;
    Client = new() { BaseAddress = baseAddress };
  }

  public string DatabasePath { get; }

  public TransactionProbe Probe { get; }

  public HttpClient Client { get; }

  public GastronomyAppDbContext CreateContext()
  {
    return _application.Services.GetRequiredService<IDbContextFactory<GastronomyAppDbContext>>().CreateDbContext();
  }

  public async ValueTask DisposeAsync()
  {
    Client.Dispose();
    await _application.StopAsync();
    await _application.DisposeAsync();
    SqliteConnection.ClearAllPools();

    foreach (var path in new[]
                         {
                           DatabasePath,
                           $"{DatabasePath}-wal",
                           $"{DatabasePath}-shm"
                         })
      File.Delete(path);
  }
}
