using GastronomyApp.Api.Options;
using GastronomyApp.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests;

[TestFixture]
public sealed class GastronomyAppApiApplicationTest
{
  private string dataDirectory = null!;

  [SetUp]
  public void SetUp()
  {
    dataDirectory = Path.Combine(Path.GetTempPath(), $"gastronomy-api-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dataDirectory);
  }

  [TearDown]
  public void TearDown()
  {
    SqliteConnection.ClearAllPools();

    if (Directory.Exists(dataDirectory))
    {
      Directory.Delete(dataDirectory, true);
    }
  }

  [Test]
  public async Task Build_TempDataDirectoryAndPortZero_ResolvesTheDatabaseContext()
  {
    ApiHostOptions options = new()
    {
      DataDirectory = dataDirectory,
      Port = 0,
      BindAddress = "127.0.0.1",
    };

    await using WebApplication application = new GastronomyAppApiApplication().Build(options);

    using IServiceScope scope = application.Services.CreateScope();
    GastronomyAppDbContext context = scope.ServiceProvider.GetRequiredService<GastronomyAppDbContext>();

    Assert.That(context, Is.Not.Null);
  }
}
