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
using GastronomyApp.Infrastructure.Ports;
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

    services.AddScoped<IOrderRepository, OrderRepository>();
    services.AddScoped<ICatalogItemRepository, CatalogItemRepository>();
    services.AddScoped<IStationRepository, StationRepository>();
    services.AddScoped<INumberAllocator, SequenceNumberAllocator>();

    services.AddSingleton<OrderRoutingResolver>();
    services.AddSingleton<OrderStatusCalculator>();
    services.AddSingleton<OrderItemSettlementService>();
    services.AddSingleton<OrderItemProductionService>();
    services.AddSingleton<ProductionEstimateCalculator>();

    services.AddScoped<OrderAcceptanceService>();
    services.AddScoped<OrderAcceptanceTransaction>();

    services.AddScoped<DeviceOwnerStore>();
    services.AddScoped<IDeviceTokenStore, DeviceTokenStore>();
    services.AddScoped<IEnrolmentInvitationStore, EnrolmentInvitationStore>();

    services.AddSingleton<CatalogReader>();
    services.AddSingleton<SavedChangeAnnouncement>();
    services.AddSingleton<CatalogChangeAnnouncer>();
    services.AddSingleton<CatalogWriteTransaction>();
    services.AddSingleton<CatalogCategoryColour>();
    services.AddSingleton<CatalogCategoryOrdering>();
    services.AddSingleton<CatalogCategoryNaming>();
    services.AddSingleton<ResultEnvelope>();
    services.AddSingleton<CallerIdentity>();
    services.AddSingleton<LocalAddressSet>();
    services.AddSingleton<LoopbackAdminAuthorizationMiddleware>();
    services.AddScoped<InfrastructureExceptionMiddleware>();

    services.AddScoped<OrderReader>();
    services.AddScoped<OrderPlacementHandler>();
    services.AddSingleton<OpenItemsReader>();
    services.AddScoped<OpenItemQueryHandler>();
    services.AddScoped<OrderItemSettlementHandler>();
    services.AddSingleton<HealthReporter>();
    services.AddSingleton<OutstandingInvitationCache>();
    services.AddScoped<InvitationQrRenderer>();
    services.AddSingleton<LocalNetworkAddressProvider>();
    services.AddSingleton<ReachableHostResolver>();
    services.AddSingleton<EnrolmentUrlBuilder>();
    services.AddScoped<DeviceRevoker>();
    services.AddScoped<OutstandingInvitationLookup>();
    services.AddSingleton<StationChangeAnnouncer>();
    services.AddScoped<AdminStationHandler>();
    services.AddScoped<AdminCategoryHandler>();
    services.AddScoped<AdminItemHandler>();
    services.AddScoped<AdminStaffMembersHandler>();
    services.AddScoped<AdminOrderHandler>();
    services.AddScoped<AdminEnrolmentHandler>();
    services.AddScoped<EnrolmentRedemptionHandler>();
    services.AddSingleton<StationShellResponder>();
    services.AddSingleton<ClientRouteFallbackResponder>();
    services.AddSingleton<StationQueueReader>();
    services.AddSingleton<DeviceKindGate>();
    services.AddScoped<StationQueryHandler>();
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
