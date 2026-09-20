using System.Text.Json;
using System.Text.Json.Serialization;
using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Auth.Callers;
using GastronomyApp.Api.Auth.Filters;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Handlers;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Mapping;
using GastronomyApp.Api.Options;
using GastronomyApp.Api.RateLimiting;
using GastronomyApp.Api.Responders;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.ErrorHandling;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Security;
using MapsterMapper;
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

    services.AddSingleton(new MappingConfiguration().Build());
    services.AddScoped<IMapper, ServiceMapper>();

    SqliteConnectionFactory connectionFactory = new();
    services.AddSingleton(connectionFactory);

    services.AddDbContextFactory<GastronomyAppDbContext>(builder => builder.UseSqlite($"Data Source={databasePath}").AddInterceptors(new SqliteConnectionPolicyInterceptor(connectionFactory)));

    services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<GastronomyAppDbContext>>().CreateDbContext());

    services.AddSingleton<IClock, SystemClock>();
    services.AddSingleton<Pbkdf2SecretHasher>();
    services.AddSingleton<SqliteFailureTranslator>();

    services.AddScoped<ImmediateTransactionRunner>();
    services.AddScoped<ITransactionRunner>(provider => provider.GetRequiredService<ImmediateTransactionRunner>());

    services.AddScoped<IOrderRepository, OrderRepository>();
    services.AddScoped<IOpenItemRepository, OpenItemRepository>();
    services.AddScoped<IDeviceRepository, DeviceRepository>();
    services.AddScoped<ICatalogItemRepository, CatalogItemRepository>();
    services.AddScoped<ICatalogCategoryRepository, CatalogCategoryRepository>();
    services.AddScoped<ICatalogRepository, CatalogRepository>();
    services.AddScoped<IItemOrderabilityRepository, ItemOrderabilityRepository>();
    services.AddScoped<IStationRepository, StationRepository>();
    services.AddScoped<IStaffMemberRepository, StaffMemberRepository>();
    services.AddScoped<IFestivalRepository, FestivalRepository>();
    services.AddScoped<IFestivalMenuRepository, FestivalMenuRepository>();
    services.AddScoped<IFestivalStationRepository, FestivalStationRepository>();
    services.AddScoped<INumberAllocator, SequenceNumberAllocator>();
    services.AddScoped<IStationOrderRepository, StationOrderRepository>();

    services.AddSingleton<OrderRoutingResolver>();
    services.AddSingleton<FestivalSchedule>();
    services.AddSingleton<FestivalMoment>();
    services.AddSingleton<OrderStatusCalculator>();
    services.AddSingleton<OrderTotalCalculator>();
    services.AddSingleton<OrderItemFulfillmentService>();
    services.AddSingleton<StationOrderVisibilityService>();
    services.AddSingleton<ProductionEstimateCalculator>();

    services.AddScoped<OrderItemResolutionService>();
    services.AddScoped<OrderItemSettlementService>();
    services.AddScoped<OpenItemsService>();
    services.AddScoped<PlacedOrderReader>();
    services.AddScoped<DeviceLanguageService>();
    services.AddScoped<OrderAcceptanceService>();
    services.AddScoped<ItemOrderability>();
    services.AddScoped<CatalogService>();
    services.AddScoped<CatalogItemAdministrationService>();
    services.AddScoped<CatalogCategoryAdministrationService>();
    services.AddScoped<DeviceOwnerRetirement>();
    services.AddScoped<FestivalAdministrationService>();
    services.AddScoped<FestivalMenuService>();
    services.AddScoped<FestivalStationService>();
    services.AddScoped<StationAdministrationService>();
    services.AddScoped<StaffMemberAdministrationService>();
    services.AddScoped<EnrolmentInvitationService>();
    services.AddScoped<RunningFestivalLookup>();
    services.AddScoped<StationAtFestivalLookup>();
    services.AddScoped<StationQueueService>();
    services.AddScoped<StationQueueWriter>();
    services.AddScoped<StationQueueChangeService>();
    services.AddScoped<OrderStatusReader>();
    services.AddScoped<StationEstimateService>();

    services.AddScoped<IDeviceOwnerStore, DeviceOwnerStore>();
    services.AddScoped<IDeviceTokenStore, DeviceTokenStore>();
    services.AddScoped<IEnrolmentInvitationStore, EnrolmentInvitationStore>();

    services.AddSingleton<SavedChangeAnnouncer>();
    services.AddSingleton<CatalogChangeAnnouncer>();
    services.AddSingleton<FestivalChangeAnnouncer>();
    services.AddSingleton<ColorFormatValidator>();
    services.AddSingleton<CatalogCategoryOrdering>();
    services.AddSingleton<ResultEnvelope>();
    services.AddSingleton<CallerIdentity>();
    services.AddSingleton<DeviceTokenSplitter>();
    services.AddSingleton<LocalAddressSet>();
    services.AddSingleton<LoopbackAdminAuthorizationMiddleware>();
    services.AddScoped<InfrastructureExceptionMiddleware>();

    services.AddScoped<OrderPlacementHandler>();
    services.AddScoped<OpenItemQueryHandler>();
    services.AddScoped<OrderItemSettlementHandler>();
    services.AddScoped<SessionHandler>();
    services.AddSingleton<OutstandingInvitationCache>();
    services.AddScoped<InvitationQRHandler>();
    services.AddSingleton<LocalNetworkAddressProvider>();
    services.AddSingleton<ReachableHostResolver>();
    services.AddSingleton<EnrolmentUrlBuilder>();
    services.AddSingleton<DeviceRevocationAnnouncer>();
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
    services.AddSingleton<DeviceKindGate>();
    services.AddSingleton<StationQueueRefusalResponder>();
    services.AddScoped<StationEstimateHandler>();
    services.AddScoped<StationQueueHandler>();
    services.AddScoped<StationFulfillmentHandler>();

    services.AddSignalR().AddJsonProtocol(protocolOptions => protocolOptions.PayloadSerializerOptions.Converters.Add(EnumsAsCamelCaseText()));
    services.AddSingleton<HubConnectionRegistry>();
    services.AddSingleton<DeviceConnectionTerminator>();
    services.AddSingleton<HubNotificationDispatcher>();

    services.ConfigureHttpJsonOptions(jsonOptions => jsonOptions.SerializerOptions.Converters.Add(EnumsAsCamelCaseText()));

    services.AddSingleton(options.Language);

    services.AddRateLimiter(limiterOptions => new RateLimitPolicies().Configure(limiterOptions));

    AuthenticationSchemeNames schemeNames = new();
    services.AddAuthentication(schemeNames.Device).AddScheme<DeviceAuthenticationSchemeOptions, DeviceAuthenticationHandler>(schemeNames.Device, null);

    services.AddAuthorization(authorization => { authorization.DefaultPolicy = new AuthorizationPolicyBuilder(schemeNames.Device).RequireAuthenticatedUser().Build(); });
  }

  private JsonStringEnumConverter EnumsAsCamelCaseText()
  {
    return new(JsonNamingPolicy.CamelCase);
  }
}
