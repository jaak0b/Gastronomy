using GastronomyApp.Api.Options;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.TestSupport;

public sealed class ApiTestFactory : IAsyncDisposable
{
  private readonly WebApplication _application;
  private readonly List<IServiceScope> _contextScopes = [];

  internal ApiTestFactory(WebApplication application, string dataDirectory, Uri baseAddress, AppLanguage language)
  {
    _application = application;
    DataDirectory = dataDirectory;
    BaseAddress = baseAddress;
    Language = language;
    Client = new() { BaseAddress = baseAddress };
  }

  public AppLanguage Language { get; }

  public string DataDirectory { get; }

  public Uri BaseAddress { get; }

  public HttpClient Client { get; }

  public IServiceProvider Services => _application.Services;

  public async ValueTask DisposeAsync()
  {
    Client.Dispose();

    foreach (var contextScope in _contextScopes)
      contextScope.Dispose();

    await _application.StopAsync();
    await _application.DisposeAsync();
    SqliteConnection.ClearAllPools();

    if (Directory.Exists(DataDirectory))
    {
      try
      {
        Directory.Delete(DataDirectory, true);
      }
      catch (IOException)
      {
        await Task.Delay(200);
        Directory.Delete(DataDirectory, true);
      }
    }
  }

  public GastronomyAppDbContext CreateContext()
  {
    var scope = Services.CreateScope();
    _contextScopes.Add(scope);
    scope.ServiceProvider.GetRequiredService<AfterCommitActions>().StartCollecting();

    return scope.ServiceProvider.GetRequiredService<IDbContextFactory<GastronomyAppDbContext>>().CreateDbContext();
  }
}
