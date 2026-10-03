using GastronomyApp.Api.Options;
using GastronomyApp.Api.Values;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.TestSupport;

public sealed class ApiTestFactoryBuilder
{
  private RateLimitOptions _rateLimits = new();

  public ApiTestFactoryBuilder WithRateLimits(RateLimitOptions rateLimits)
  {
    _rateLimits = rateLimits;

    return this;
  }

  public async Task<ApiTestFactory> StartAsync()
  {
    var dataDirectory = Path.Combine(Path.GetTempPath(), $"gastronomy-api-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dataDirectory);

    AppLanguage language = new();
    var application = new GastronomyAppApiApplication().Build(new()
                                                              {
                                                                DataDirectory = dataDirectory,
                                                                Port = 0,
                                                                BindAddress = "127.0.0.1",
                                                                Language = language,
                                                                RateLimits = _rateLimits
                                                              });

    await application.StartAsync();

    var addresses = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
    Uri baseAddress = new(addresses.Addresses.First());

    return new(application, dataDirectory, baseAddress, language);
  }
}
