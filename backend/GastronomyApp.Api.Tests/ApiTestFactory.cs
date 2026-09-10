using GastronomyApp.Api.Options;
using GastronomyApp.Core.Services;
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
  Guid FoodCategoryId,
  Guid DrinkCategoryId,
  Guid BratwurstItemId,
  Guid BeerItemId,
  Guid FestivalId);

public sealed class ApiSeeder
{
  private readonly DateTime _baseline = new(2026, 8, 26, 19, 40, 0, DateTimeKind.Utc);
  private readonly CatalogCategoryNaming _naming = new();

  public async Task<SeededWorld> SeedAsync(GastronomyAppDbContext context, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(context);

    SeededWorld world = new(Guid.NewGuid(),
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            Guid.NewGuid());

    context.Festivals.Add(new()
                          {
                            Id = world.FestivalId,
                            Name = "Sommerfest",
                            StartsAtUtc = DateTime.UtcNow.AddDays(-1),
                            EndsAtUtc = DateTime.UtcNow.AddYears(1),
                            NextOrderNumber = 1,
                            IsHidden = false
                          });

    context.StaffMembers.Add(new()
                             {
                               Id = world.StaffMemberId,
                               Name = "Anna",
                               IsActive = true,
                               CreatedAtUtc = _baseline
                             });

    AddStation(context, world, world.KitchenStationId, "Kueche", 1);
    AddStation(context, world, world.BarStationId, "Bar", 2);

    AddCategory(context, world.FoodCategoryId, "Essen", "#C62828", 1);
    AddCategory(context, world.DrinkCategoryId, "Getraenke", "#1565C0", 2);

    AddItem(context, world, world.BratwurstItemId, "Bratwurst mit Brot", world.FoodCategoryId, 350, 1, world.KitchenStationId);
    AddItem(context, world, world.BeerItemId, "Bier", world.DrinkCategoryId, 300, 2, world.BarStationId);

    await context.SaveChangesAsync(cancellationToken);
    return world;
  }

  private void AddStation(GastronomyAppDbContext context,
                          SeededWorld world,
                          Guid stationId,
                          string name,
                          int sortOrder)
  {
    context.Stations.Add(new()
                         {
                           Id = stationId,
                           Name = name,
                           SortOrder = sortOrder,
                           IsActive = true
                         });

    context.FestivalStations.Add(new()
                                 {
                                   Id = Guid.NewGuid(),
                                   FestivalId = world.FestivalId,
                                   StationId = stationId,
                                   NextStationOrderNumber = 1
                                 });
  }

  private void AddCategory(GastronomyAppDbContext context,
                           Guid categoryId,
                           string name,
                           string colourHex,
                           int sortOrder)
  {
    context.CatalogCategories.Add(new()
                                  {
                                    Id = categoryId,
                                    Name = name,
                                    NormalizedName = _naming.Normalized(name),
                                    ColourHex = colourHex,
                                    SortOrder = sortOrder,
                                    IsActive = true
                                  });
  }

  private void AddItem(GastronomyAppDbContext context,
                       SeededWorld world,
                       Guid itemId,
                       string name,
                       Guid categoryId,
                       int priceCents,
                       int sortOrder,
                       Guid stationId)
  {
    context.CatalogItems.Add(new()
                             {
                               Id = itemId,
                               Name = name,
                               CategoryId = categoryId,
                               IsActive = true,
                               SortOrder = sortOrder
                             });

    context.FestivalCatalogItems.Add(new()
                                     {
                                       Id = Guid.NewGuid(),
                                       FestivalId = world.FestivalId,
                                       CatalogItemId = itemId,
                                       PriceCents = priceCents,
                                       IsAvailable = true
                                     });

    context.ItemStationAssignments.Add(new()
                                       {
                                         Id = Guid.NewGuid(),
                                         FestivalId = world.FestivalId,
                                         CatalogItemId = itemId,
                                         StationId = stationId
                                       });
  }
}
