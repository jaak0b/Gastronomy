using GastronomyApp.Api.Options;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests;

public sealed class ApiTestFactoryBuilder
{
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
                                                                Language = language
                                                              });

    await application.StartAsync();

    var addresses = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
    Uri baseAddress = new(addresses.Addresses.First());

    return new(application, dataDirectory, baseAddress, language);
  }
}
