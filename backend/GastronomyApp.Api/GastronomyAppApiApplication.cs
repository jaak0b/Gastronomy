using GastronomyApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using GastronomyApp.Api.Values;

namespace GastronomyApp.Api;

public sealed class GastronomyAppApiApplication
{
  public WebApplication Build(ApiHostOptions options)
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });

    builder.WebHost.UseUrls($"http://{options.BindAddress}:{options.Port}");
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();

    new ApiServiceRegistration().Register(builder.Services, options);

    var app = builder.Build();

    MigrateTheDatabase(app.Services);
    new ApiPipeline().Configure(app);

    return app;
  }

  private void MigrateTheDatabase(IServiceProvider services)
  {
    using var scope = services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<GastronomyAppDbContext>();

    database.Database.Migrate();
  }
}
