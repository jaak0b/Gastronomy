using GastronomyApp.Api.Options;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace GastronomyApp.Api;

public sealed class GastronomyAppApiApplication
{
  public WebApplication Build(ApiHostOptions options)
  {
    WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
      ContentRootPath = AppContext.BaseDirectory,
    });

    builder.WebHost.UseUrls($"http://{options.BindAddress}:{options.Port}");
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();

    new ApiServiceRegistration().Register(builder.Services, options);

    WebApplication app = builder.Build();

    new DatabaseInitializer().Initialize(app.Services);
    new ApiPipeline().Configure(app);

    return app;
  }
}

public sealed class DatabaseInitializer
{
  public void Initialize(IServiceProvider services)
  {
    using IServiceScope scope = services.CreateScope();
    GastronomyAppDbContext context = scope.ServiceProvider.GetRequiredService<GastronomyAppDbContext>();
    context.Database.Migrate();
  }
}
