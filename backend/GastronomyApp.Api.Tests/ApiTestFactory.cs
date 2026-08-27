using GastronomyApp.Api.Options;
using GastronomyApp.Api.Printing;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
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
    private readonly WebApplication application;

    private ApiTestFactory(
        WebApplication application,
        string dataDirectory,
        Uri baseAddress,
        AppLanguage language)
    {
        this.application = application;
        DataDirectory = dataDirectory;
        BaseAddress = baseAddress;
        Language = language;
        Client = new HttpClient { BaseAddress = baseAddress };
    }

    public AppLanguage Language { get; }

    public string DataDirectory { get; }

    public Uri BaseAddress { get; }

    public HttpClient Client { get; }

    public IServiceProvider Services
    {
        get { return application.Services; }
    }

    public string MockSlipFolder
    {
        get { return Path.Combine(DataDirectory, "mock-slips"); }
    }

    public Task ReconcilePrintersAsync()
    {
        return Services.GetRequiredService<PrinterFleet>().ReconcileAsync(CancellationToken.None);
    }

    public GastronomyAppDbContext CreateContext()
    {
        return Services.GetRequiredService<IDbContextFactory<GastronomyAppDbContext>>().CreateDbContext();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await application.StopAsync();
        await application.DisposeAsync();
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

    public sealed class Builder
    {
        public async Task<ApiTestFactory> StartAsync()
        {
            string dataDirectory = Path.Combine(Path.GetTempPath(), $"gastronomy-api-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dataDirectory);

            AppLanguage language = new();
            WebApplication application = new GastronomyAppApiApplication().Build(new ApiHostOptions
            {
                DataDirectory = dataDirectory,
                Port = 0,
                BindAddress = "127.0.0.1",
                Language = language,
            });

            await application.StartAsync();

            IServerAddressesFeature addresses =
                application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
            Uri baseAddress = new(addresses.Addresses.First());

            return new ApiTestFactory(application, dataDirectory, baseAddress, language);
        }
    }
}

public sealed record SeededWorld(
    Guid EventSessionId,
    Guid StaffMemberId,
    Guid KitchenLocationId,
    Guid BarLocationId,
    Guid BratwurstItemId,
    Guid BeerItemId);

public sealed class ApiSeeder
{
    private readonly DateTime baseline = new(2026, 8, 26, 19, 40, 0, DateTimeKind.Utc);

    public async Task<SeededWorld> SeedAsync(GastronomyAppDbContext context, CancellationToken cancellationToken)
    {
        SeededWorld world = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());


        context.StaffMembers.Add(new StaffMember
        {
            Id = world.StaffMemberId,
            Name = "Anna",
            IsActive = true,
            CreatedAtUtc = baseline,
        });

        AddLocation(context, world.KitchenLocationId, "Kueche", 1);
        AddLocation(context, world.BarLocationId, "Bar", 2);

        AddItem(context, world.BratwurstItemId, "Bratwurst mit Brot", "Essen", 350, 1, world.KitchenLocationId);
        AddItem(context, world.BeerItemId, "Bier", "Getraenke", 300, 2, world.BarLocationId);

        context.TableSuggestions.Add(new TableSuggestion
        {
            Id = Guid.NewGuid(),
            Label = "Tisch 12",
            SortOrder = 1,
        });

        await context.SaveChangesAsync(cancellationToken);
        return world;
    }

    private void AddLocation(GastronomyAppDbContext context, Guid locationId, string name, int sortOrder)
    {
        context.ProductionLocations.Add(new ProductionLocation
        {
            Id = locationId,
            Name = name,
            SortOrder = sortOrder,
            IsActive = true,
        });

        context.PrinterConfigurations.Add(new PrinterConfiguration
        {
            ProductionLocationId = locationId,
            TransportKind = TransportKind.Mock,
            Host = null,
            Port = 0,
            AgentIdentifier = null,
            CharactersPerLine = 48,
            CodePageName = "PC858",
            ConnectTimeoutSeconds = 3,
            JobTimeoutSeconds = 90,
            HeartbeatSeconds = 10,
            IsEnabled = true,
        });

        context.PrinterStatuses.Add(new PrinterStatus
        {
            ProductionLocationId = locationId,
            IsOnline = true,
            IsPaperEnd = false,
            IsPaperNearEnd = false,
            IsCoverOpen = false,
            IsInErrorState = false,
            IsFaulty = false,
            LastDetail = "seeded",
            LastChangedAtUtc = baseline,
            LastHeardFromAtUtc = baseline,
        });
    }

    private void AddItem(
        GastronomyAppDbContext context,
        Guid itemId,
        string name,
        string categoryName,
        int priceCents,
        int sortOrder,
        Guid locationId)
    {
        context.CatalogItems.Add(new CatalogItem
        {
            Id = itemId,
            Name = name,
            CategoryName = categoryName,
            PriceCents = priceCents,
            SortOrder = sortOrder,
            IsActive = true,
            IsAvailable = true,
        });

        context.ItemLocationAssignments.Add(new ItemLocationAssignment
        {
            Id = Guid.NewGuid(),
            CatalogItemId = itemId,
            ProductionLocationId = locationId,
        });
    }
}
