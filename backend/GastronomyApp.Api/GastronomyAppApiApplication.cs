using GastronomyApp.Api.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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

    new DatabaseInitializer().Initialize(app.Services);
    new ApiPipeline().Configure(app);

    return app;
  }
}
