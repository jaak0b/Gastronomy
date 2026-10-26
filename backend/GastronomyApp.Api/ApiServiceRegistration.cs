using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using GastronomyApp.Api.Auth.Filters;
using GastronomyApp.Api.Auth;
using GastronomyApp.Api.DocumentTransformers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Json;
using GastronomyApp.Api.Mapping;
using GastronomyApp.Api.RateLimiting;
using GastronomyApp.Api.Responders;
using GastronomyApp.Api.Values;
using GastronomyApp.Core.Announcements;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.ErrorHandling;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Security;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

    services.AddDbContextFactory<GastronomyAppDbContext>((provider, builder) => builder.UseSqlite($"Data Source={databasePath}").AddInterceptors(new SqliteConnectionPolicyInterceptor(connectionFactory), provider.GetRequiredService<ChangeAnnouncementInterceptor>()), ServiceLifetime.Scoped);

    services.AddScoped<ChangeAnnouncementInterceptor>();
    services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<GastronomyAppDbContext>>().CreateDbContext());
    services.AddScoped<IGastronomyDatabase>(provider => provider.GetRequiredService<GastronomyAppDbContext>());

    services.AddSingleton<Pbkdf2SecretHasher>();
    services.AddSingleton<ISecretHasher>(provider => provider.GetRequiredService<Pbkdf2SecretHasher>());
    services.AddSingleton<SqliteFailureTranslator>();

    services.AddScoped<AfterCommitActions>();
    services.AddScoped<IAfterCommitActions>(provider => provider.GetRequiredService<AfterCommitActions>());

    services.AddScoped<IFestivalRepository, FestivalRepository>();

    services.AddSingleton<OrderRoutingResolver>();
    services.AddSingleton<OrderService>();
    services.AddSingleton<StationWaitingTimes>();
    services.AddSingleton<TableProgress>();
    services.AddSingleton<FestivalSchedule>();
    services.AddSingleton<AreaShapeService>();
    services.AddSingleton<TableCopyNaming>();

    services.AddScoped<OpenItemsService>();
    services.AddScoped<TablePlanService>();
    services.AddScoped<OrderPlacementService>();
    services.AddScoped<SequenceNumberAllocator>();
    services.AddScoped<ItemOrderability>();
    services.AddScoped<CatalogService>();
    services.AddScoped<CatalogCategoryService>();
    services.AddScoped<CatalogItemService>();
    services.AddScoped<DeviceOwnerLookup>();
    services.AddScoped<DeviceOwnerRetirement>();
    services.AddScoped<DeviceSessionService>();
    services.AddScoped<FestivalService>();
    services.AddScoped<FestivalMenuService>();
    services.AddScoped<FestivalStationService>();
    services.AddScoped<FestivalRowHiding>();
    services.AddScoped<AreaEditService>();
    services.AddScoped<TableEditService>();
    services.AddScoped<StationService>();
    services.AddScoped<StaffMemberService>();
    services.AddScoped<EnrolmentService>();
    services.AddScoped<RunningFestivalLookup>();
    services.AddScoped<ItemsToHandOutLookup>();
    services.AddScoped<StationQueueService>();
    services.AddScoped<EstimateService>();
    services.AddScoped<IngredientService>();
    services.AddScoped<ActivationService>();
    services.AddScoped<UnitService>();
    services.AddScoped<FestivalStockService>();
    services.AddScoped<StockSoldOutMarker>();
    services.AddSingleton<IngredientAmountComparison>();
    services.AddSingleton<StockForecast>();

    services.AddSingleton<SortOrdering>();
    services.AddHttpContextAccessor();
    services.AddSingleton<SystemTextJsonRecordingSerializer>();
    services.AddSingleton<ResultEnvelope>();
    services.AddSingleton<IProblemDetailsService, RequestShapeRefusalWriter>();
    services.AddScoped<CallerIdentity>();
    services.AddSingleton<DeviceTokenSplitter>();
    services.AddSingleton<IDeviceTokenSplitter>(provider => provider.GetRequiredService<DeviceTokenSplitter>());
    services.AddSingleton<LocalAddressSet>();
    services.AddSingleton<LoopbackAdminAuthorizationMiddleware>();
    services.AddScoped<InfrastructureExceptionMiddleware>();

    services.AddSingleton<OutstandingInvitationCache>();
    services.AddSingleton<IOutstandingInvitationCache>(provider => provider.GetRequiredService<OutstandingInvitationCache>());
    services.AddSingleton<LocalNetworkAddressProvider>();
    services.AddSingleton<ReachableHostResolver>();
    services.AddSingleton<EnrolmentUrlBuilder>();
    services.AddSingleton<IEnrolmentUrlBuilder>(provider => provider.GetRequiredService<EnrolmentUrlBuilder>());
    services.AddSingleton<InvitationQRResponder>();
    services.AddSingleton<StationShellResponder>();
    services.AddSingleton<ClientRouteFallbackResponder>();
    services.AddSingleton<DeviceKindGate>();

    services.AddValidation();

    services.AddSignalR().AddJsonProtocol(protocolOptions => protocolOptions.PayloadSerializerOptions.Converters.Add(EnumsAsCamelCaseText()));
    services.AddSingleton<HubConnectionRegistry>();
    services.AddSingleton<DeviceConnectionTerminator>();
    services.AddSingleton<IAnnouncementGuard, AnnouncementGuard>();
    services.AddScoped<HubNotificationDispatcher>();
    services.AddScoped<ICommittedChangeAnnouncer>(provider => provider.GetRequiredService<HubNotificationDispatcher>());
    services.AddScoped<IEnrolmentCompletionAnnouncer>(provider => provider.GetRequiredService<HubNotificationDispatcher>());
    services.AddScoped<IDeviceRevocationAnnouncer>(provider => provider.GetRequiredService<HubNotificationDispatcher>());

    services.ConfigureHttpJsonOptions(jsonOptions =>
                                      {
                                        jsonOptions.SerializerOptions.Converters.Add(EnumsAsCamelCaseText());
                                        jsonOptions.SerializerOptions.TypeInfoResolver = (jsonOptions.SerializerOptions.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithAddedModifier(new TrimmedStringModifier().Trim);
                                      });

    services.AddOpenApi(documentOptions =>
                        {
                          documentOptions.AddSchemaTransformer(new StringEncodedNumberSchemaTransformer());
                          documentOptions.AddDocumentTransformer(new HubEventSchemaTransformer());
                        });

    services.AddSingleton(options.Language);

    services.AddRateLimiter(limiterOptions => new RateLimitPolicies(options.RateLimits).Configure(limiterOptions));

    services.AddAuthentication(Names.AuthenticationSchemes.Device).AddScheme<DeviceAuthenticationSchemeOptions, DeviceAuthenticationHandler>(Names.AuthenticationSchemes.Device, null);

    services.AddAuthorization(authorization => { authorization.DefaultPolicy = new AuthorizationPolicyBuilder(Names.AuthenticationSchemes.Device).RequireAuthenticatedUser().Build(); });
  }

  private JsonStringEnumConverter EnumsAsCamelCaseText()
  {
    return new(JsonNamingPolicy.CamelCase);
  }
}
