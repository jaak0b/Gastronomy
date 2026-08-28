using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Endpoints;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Printing;
using GastronomyApp.Api.RateLimiting;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Options;
using GastronomyApp.Core.Localization;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Printing;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Ports;
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
        Directory.CreateDirectory(options.DataDirectory);
        string databasePath = Path.Combine(options.DataDirectory, DatabaseFileName);

        services.AddSingleton(options);
        services.AddSingleton(TimeProvider.System);

        SqliteConnectionFactory connectionFactory = new();
        services.AddSingleton(connectionFactory);

        services.AddDbContextFactory<GastronomyAppDbContext>(
            builder => builder
                .UseSqlite($"Data Source={databasePath}")
                .AddInterceptors(new SqliteConnectionPolicyInterceptor(connectionFactory)),
            ServiceLifetime.Singleton);

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
        services.AddSingleton<HandledOnPaperPolicy>();
        services.AddSingleton<PrintJobStateMachine>();
        services.AddSingleton<RetryPolicy>();
        services.AddSingleton<GiveUpWindowCalculator>();

        services.AddScoped<OrderAcceptanceService>();
        services.AddScoped<OrderAcceptanceTransaction>();

        services.AddScoped<IDeviceTokenStore, DeviceTokenStore>();
        services.AddScoped<IEnrolmentInvitationStore, EnrolmentInvitationStore>();

        services.AddSingleton<CatalogReader>();
        services.AddSingleton<ResultEnvelope>();
        services.AddSingleton<CallerIdentity>();
        services.AddSingleton<LocalAddressSet>();
        services.AddSingleton<LoopbackAdminAuthorizationMiddleware>();
        services.AddScoped<InfrastructureExceptionMiddleware>();

        services.AddScoped<PrintJobEnqueuer>();
        services.AddScoped<OrderReader>();
        services.AddScoped<OrderPlacementHandler>();
        services.AddScoped<OrderQueryHandler>();
        services.AddScoped<StationOrderActionHandler>();
        services.AddSingleton<PrinterStatusReader>();
        services.AddSingleton<HealthReporter>();
        services.AddSingleton<OutstandingInvitationCache>();
        services.AddScoped<InvitationQrRenderer>();
        services.AddSingleton<LocalNetworkAddressProvider>();
        services.AddSingleton<ReachableHostResolver>();
        services.AddSingleton<EnrolmentUrlBuilder>();
        services.AddScoped<AdminStationHandler>();
        services.AddScoped<AdminItemHandler>();
        services.AddScoped<AdminStaffMembersHandler>();
        services.AddScoped<AdminPrinterHandler>();
        services.AddScoped<AdminOrderHandler>();
        services.AddScoped<EnrolmentRedemptionHandler>();
        services.AddSingleton<StationPrintabilityReader>();
        services.AddSingleton<StationScreenDescriber>();
        services.AddSingleton<StationShellResponder>();
        services.AddSingleton<ClientRouteFallbackResponder>();
        services.AddScoped<StationQueryHandler>();
        services.AddScoped<StationHandOnPaperHandler>();

        services.AddSignalR();
        services.AddSingleton<HubConnectionRegistry>();
        services.AddSingleton<DeviceConnectionTerminator>();
        services.AddSingleton<HubNotificationDispatcher>();
        services.AddSingleton<IPrintCallbacks>(provider => provider.GetRequiredService<HubNotificationDispatcher>());

        services.AddSingleton(options.Language);
        services.AddSingleton<IMockFaultRegistry, InMemoryMockFaultRegistry>();
        services.AddSingleton(provider => new TestPrinterDriver(
            options.DataDirectory,
            provider.GetRequiredService<IMockFaultRegistry>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IPrinterDriver>(provider => provider.GetRequiredService<TestPrinterDriver>());
        services.AddSingleton<EpsonTmT20ivNetworkPrinterDriver>();
        services.AddSingleton<IPrinterDriver>(provider =>
            provider.GetRequiredService<EpsonTmT20ivNetworkPrinterDriver>());
        services.AddSingleton(provider => new PrinterDriverRegistry(
            provider.GetServices<IPrinterDriver>()));
        services.AddSingleton<IPrinterSource, DatabasePrinterSource>();
        services.AddSingleton<ISlipTextProvider, GastronomyApp.Infrastructure.Localization.ResxSlipTextProvider>();
        services.AddSingleton<EscPosSlipRenderer>();
        services.AddSingleton(provider => new PrinterWorkerDomainServices(
            provider.GetRequiredService<RetryPolicy>(),
            provider.GetRequiredService<GiveUpWindowCalculator>(),
            provider.GetRequiredService<OrderStatusCalculator>(),
            provider.GetRequiredService<PrintJobStateMachine>()));
        services.AddSingleton<IPrinterWorkerDataAccess>(provider => new EfCorePrinterWorkerDataAccess(
            () => provider.GetRequiredService<IDbContextFactory<GastronomyAppDbContext>>().CreateDbContext(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<StationPrinterStatusLookup>();
        services.AddSingleton<PrinterFleet>();
        services.AddSingleton<IPrinterFleet>(provider => provider.GetRequiredService<PrinterFleet>());
        services.AddHostedService(provider => provider.GetRequiredService<PrinterFleet>());

        services.AddRateLimiter(limiterOptions => new RateLimitPolicies().Configure(limiterOptions));

        AuthenticationSchemeNames schemeNames = new();
        services.AddAuthentication(schemeNames.Device)
            .AddScheme<DeviceAuthenticationSchemeOptions, DeviceAuthenticationHandler>(
                schemeNames.Device,
                configureOptions: null);

        services.AddAuthorization(authorization =>
        {
            authorization.DefaultPolicy = new AuthorizationPolicyBuilder(schemeNames.Device)
                .RequireAuthenticatedUser()
                .Build();
        });
    }
}
