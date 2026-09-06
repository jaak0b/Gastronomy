using GastronomyApp.Api.Options;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests;

public sealed class ApiTestFactory : IAsyncDisposable
{
  private readonly WebApplication _application;

  private ApiTestFactory(WebApplication application,
                         string dataDirectory,
                         Uri baseAddress,
                         AppLanguage language)
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
    return Services.GetRequiredService<IDbContextFactory<GastronomyAppDbContext>>().CreateDbContext();
  }

  public sealed class Builder
  {
    public async Task<ApiTestFactory> StartAsync()
    {
      var dataDirectory = Path.Combine(Path.GetTempPath(), $"gastronomy-api-{Guid.NewGuid():N}");
      Directory.CreateDirectory(dataDirectory);

      AppLanguage language = new();
      var application = new GastronomyAppApiApplication().Build(new()
                                                               {
                                                                 DataDirectory = dataDirectory,
                                                                 Port = 0,
                                                                 BindAddress = "127.0.0.1",
                                                                 Language = language
                                                               });

      await application.StartAsync();

      var addresses =
        application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
      Uri baseAddress = new(addresses.Addresses.First());

      return new(application, dataDirectory, baseAddress, language);
    }
  }
}

public sealed record SeededWorld(
  Guid StaffMemberId,
  Guid KitchenStationId,
  Guid BarStationId,
  Guid BratwurstItemId,
  Guid BeerItemId);

public sealed class ApiSeeder
{
  private readonly DateTime _baseline = new(2026, 8, 26, 19, 40, 0, DateTimeKind.Utc);

  public async Task<SeededWorld> SeedAsync(GastronomyAppDbContext context, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(context);

    SeededWorld world = new(Guid.NewGuid(),
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            Guid.NewGuid());

    context.StaffMembers.Add(new()
                             {
                               Id = world.StaffMemberId,
                               Name = "Anna",
                               IsActive = true,
                               CreatedAtUtc = _baseline
                             });

    AddStation(context, world.KitchenStationId, "Kueche", 1);
    AddStation(context, world.BarStationId, "Bar", 2);

    AddItem(context, world.BratwurstItemId, "Bratwurst mit Brot", "Essen", 350, 1, world.KitchenStationId);
    AddItem(context, world.BeerItemId, "Bier", "Getraenke", 300, 2, world.BarStationId);

    await context.SaveChangesAsync(cancellationToken);
    return world;
  }

  private void AddStation(GastronomyAppDbContext context, Guid stationId, string name, int sortOrder)
  {
    context.Stations.Add(new()
                         {
                           Id = stationId,
                           Name = name,
                           SortOrder = sortOrder,
                           IsActive = true,
                           NextStationOrderNumber = 1
                         });
  }

  private void AddItem(GastronomyAppDbContext context,
                       Guid itemId,
                       string name,
                       string categoryName,
                       int priceCents,
                       int sortOrder,
                       Guid stationId)
  {
    context.CatalogItems.Add(new()
                             {
                               Id = itemId,
                               Name = name,
                               CategoryName = categoryName,
                               PriceCents = priceCents,
                               IsActive = true,
                               SortOrder = sortOrder,
                               IsAvailable = true
                             });

    context.ItemStationAssignments.Add(new()
                                       {
                                         Id = Guid.NewGuid(),
                                         CatalogItemId = itemId,
                                         StationId = stationId
                                       });
  }
}
