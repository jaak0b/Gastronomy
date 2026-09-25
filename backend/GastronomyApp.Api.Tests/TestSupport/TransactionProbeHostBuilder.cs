using ErrorOr;
using FakeItEasy;
using GastronomyApp.Api.Answers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Filters;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Core.Announcements;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.ErrorHandling;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Tests.TestSupport;

public sealed class TransactionProbeHostBuilder
{
  public async Task<TransactionProbeHost> StartAsync()
  {
    var databasePath = Path.Combine(Path.GetTempPath(), $"gastronomy-transaction-{Guid.NewGuid():N}.db");
    TransactionProbe probe = new();
    var builder = WebApplication.CreateBuilder();

    builder.Logging.ClearProviders();
    builder.WebHost.UseUrls("http://127.0.0.1:0");

    SqliteConnectionFactory connectionFactory = new();
    builder.Services.AddSingleton(connectionFactory);
    builder.Services.AddSingleton(probe);
    builder.Services.AddSingleton<SqliteFailureTranslator>();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton<SystemTextJsonRecordingSerializer>();
    builder.Services.AddSingleton<ResultEnvelope>();
    builder.Services.AddDbContextFactory<GastronomyAppDbContext>(contextOptions => contextOptions.UseSqlite($"Data Source={databasePath}").AddInterceptors(new SqliteConnectionPolicyInterceptor(connectionFactory)));
    builder.Services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<GastronomyAppDbContext>>().CreateDbContext());
    builder.Services.AddSingleton(A.Fake<ICommittedChangeAnnouncer>());
    builder.Services.AddScoped<AfterCommitActions>();
    builder.Services.AddScoped<IAfterCommitActions>(provider => provider.GetRequiredService<AfterCommitActions>());

    var application = builder.Build();

    await using (var migrationContext = application.Services.GetRequiredService<IDbContextFactory<GastronomyAppDbContext>>().CreateDbContext())
      await migrationContext.Database.MigrateAsync();

    application.Use(async (httpContext, nextMiddleware) =>
                    {
                      try
                      {
                        await nextMiddleware(httpContext);
                      }
                      catch (Exception exception)
                      {
                        probe.Failure = exception;
                        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                      }
                    });

    var group = application.MapGroup("/probe").AddEndpointFilter<RequestTransactionFilter>();

    group.MapPost(string.Empty, async Task<ApiAnswer<Guid>> (TransactionProbe requestProbe, GastronomyAppDbContext database, IAfterCommitActions afterCommitActions, CancellationToken cancellationToken) => await requestProbe.Handle(database, afterCommitActions, cancellationToken));

    group.MapPost("/created", async Task<CreatedAnswer<Guid>> (TransactionProbe requestProbe, GastronomyAppDbContext database, IAfterCommitActions afterCommitActions, CancellationToken cancellationToken) => await requestProbe.Handle(database, afterCommitActions, cancellationToken));

    group.MapPost("/no-content", async Task<NoContentAnswer> (TransactionProbe requestProbe, GastronomyAppDbContext database, IAfterCommitActions afterCommitActions, CancellationToken cancellationToken) => (await requestProbe.Handle(database, afterCommitActions, cancellationToken)).Then<Success>(_ => Result.Success));

    group.MapGet(string.Empty, (GastronomyAppDbContext database) => Results.Text(database.Database.AutoTransactionBehavior.ToString()));

    await application.StartAsync();

    var addresses = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;

    return new(application, probe, databasePath, new(addresses.Addresses.First()));
  }
}
