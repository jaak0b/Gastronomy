using System.Text.Json.Serialization;
using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Endpoints;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Options;
using GastronomyApp.Api.RateLimiting;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api;

public sealed class ApiServiceRegistration
{
  private const string DatabaseFileName = "gastronomy.db";

  public void Register(IServiceCollection services, ApiHostOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);

    Directory.CreateDirectory(options.DataDirectory);
    var databasePath = Path.Combine(options.DataDirectory, DatabaseFileName);

    services.AddSingleton(options);
    services.AddSingleton(TimeProvider.System);

    SqliteConnectionFactory connectionFactory = new();
    services.AddSingleton(connectionFactory);

    services.AddDbContextFactory<GastronomyAppDbContext>(builder => builder
                                                                   .UseSqlite($"Data Source={databasePath}")
                                                                   .AddInterceptors(new SqliteConnectionPolicyInterceptor(connectionFactory)));

    services.AddScoped(provider =>
                         provider.GetRequiredService<IDbContextFactory<GastronomyAppDbContext>>().CreateDbContext());

    services.AddSingleton<IClock, SystemClock>();
    services.AddSingleton<Pbkdf2SecretHasher>();
    services.AddSingleton<SqliteFailureTranslator>();

    services.AddScoped<ImmediateTransactionRunner>();
    services.AddScoped<ITransactionRunner>(provider => provider.GetRequiredService<ImmediateTransactionRunner>());

    services.AddScoped<IOrderRepository, OrderRepository>();
    services.AddScoped<ICatalogItemRepository, CatalogItemRepository>();
    services.AddScoped<ICatalogCategoryRepository, CatalogCategoryRepository>();
    services.AddScoped<ICatalogRepository, CatalogRepository>();
    services.AddScoped<IItemOrderabilityRepository, ItemOrderabilityRepository>();
    services.AddScoped<IStationRepository, StationRepository>();
    services.AddScoped<IFestivalRepository, FestivalRepository>();
    services.AddScoped<INumberAllocator, SequenceNumberAllocator>();

    services.AddSingleton<OrderRoutingResolver>();
    services.AddSingleton<FestivalSchedule>();
    services.AddSingleton<FestivalMoment>();
    services.AddSingleton<OrderStatusCalculator>();
    services.AddSingleton<OrderItemSettlementService>();
    services.AddSingleton<OrderItemFulfillmentService>();
    services.AddSingleton<StationOrderVisibilityService>();
    services.AddSingleton<ProductionEstimateCalculator>();

    services.AddScoped<OrderItemResolutionService>();
    services.AddScoped<OrderAcceptanceService>();
    services.AddScoped<ItemOrderability>();
    services.AddScoped<CatalogService>();
    services.AddScoped<CatalogItemAdministrationService>();
    services.AddScoped<CatalogCategoryAdministrationService>();

    services.AddScoped<IDeviceOwnerStore, DeviceOwnerStore>();
    services.AddScoped<IDeviceTokenStore, DeviceTokenStore>();
    services.AddScoped<IEnrolmentInvitationStore, EnrolmentInvitationStore>();

    services.AddSingleton<SavedChangeAnnouncement>();
    services.AddSingleton<CatalogChangeAnnouncer>();
    services.AddScoped<CatalogWriteTransaction>();
    services.AddSingleton<ColorFormatValidator>();
    services.AddSingleton<CatalogCategoryOrdering>();
    services.AddSingleton<ResultEnvelope>();
    services.AddSingleton<CallerIdentity>();
    services.AddSingleton<DeviceTokenSplitter>();
    services.AddSingleton<LocalAddressSet>();
    services.AddSingleton<LoopbackAdminAuthorizationMiddleware>();
    services.AddScoped<InfrastructureExceptionMiddleware>();

    services.AddScoped<OrderReader>();
    services.AddScoped<OrderPlacementHandler>();
    services.AddSingleton<OpenItemsReader>();
    services.AddScoped<OpenItemQueryHandler>();
    services.AddScoped<OrderItemSettlementHandler>();
    services.AddSingleton<OutstandingInvitationCache>();
    services.AddScoped<InvitationQRRenderer>();
    services.AddSingleton<LocalNetworkAddressProvider>();
    services.AddSingleton<ReachableHostResolver>();
    services.AddSingleton<EnrolmentUrlBuilder>();
    services.AddScoped<DeviceRevoker>();
    services.AddScoped<OutstandingInvitationLookup>();
    services.AddSingleton<StationChangeAnnouncer>();
    services.AddScoped<AdminStationHandler>();
    services.AddScoped<AdminFestivalHandler>();
    services.AddScoped<AdminFestivalMenuHandler>();
    services.AddScoped<AdminFestivalStationHandler>();
    services.AddScoped<AdminCategoryHandler>();
    services.AddScoped<AdminItemHandler>();
    services.AddScoped<CatalogHandler>();
    services.AddScoped<AdminStaffMembersHandler>();
    services.AddScoped<AdminEnrolmentHandler>();
    services.AddScoped<EnrolmentRedemptionHandler>();
    services.AddSingleton<StationShellResponder>();
    services.AddSingleton<ClientRouteFallbackResponder>();
    services.AddSingleton<StationQueueReader>();
    services.AddSingleton<StationsAtTheFestivalReader>();
    services.AddSingleton<DeviceKindGate>();
    services.AddScoped<StationEstimateHandler>();
    services.AddScoped<StationQueueHandler>();

    services.AddSignalR()
            .AddJsonProtocol(protocolOptions =>
                               protocolOptions.PayloadSerializerOptions.Converters.Add(EnumsAsCamelCaseText()));
    services.AddSingleton<HubConnectionRegistry>();
    services.AddSingleton<DeviceConnectionTerminator>();
    services.AddSingleton<HubNotificationDispatcher>();

    services.ConfigureHttpJsonOptions(jsonOptions =>
                                        jsonOptions.SerializerOptions.Converters.Add(EnumsAsCamelCaseText()));

    services.AddSingleton(options.Language);

    services.AddRateLimiter(limiterOptions => new RateLimitPolicies().Configure(limiterOptions));

    AuthenticationSchemeNames schemeNames = new();
    services.AddAuthentication(schemeNames.Device)
            .AddScheme<DeviceAuthenticationSchemeOptions, DeviceAuthenticationHandler>(schemeNames.Device,
                                                                                       null);

    services.AddAuthorization(authorization =>
                              {
                                authorization.DefaultPolicy = new AuthorizationPolicyBuilder(schemeNames.Device)
                                                             .RequireAuthenticatedUser()
                                                             .Build();
                              });
  }

  private JsonStringEnumConverter EnumsAsCamelCaseText()
  {
    return new(System.Text.Json.JsonNamingPolicy.CamelCase);
  }
}
