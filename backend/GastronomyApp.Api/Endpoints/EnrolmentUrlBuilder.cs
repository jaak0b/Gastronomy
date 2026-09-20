using GastronomyApp.Api.Hosting;
using GastronomyApp.Api.Options;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace GastronomyApp.Api.Endpoints;

public sealed class EnrolmentUrlBuilder
{
  private readonly ApiHostOptions _hostOptions;
  private readonly ReachableHostResolver _hostResolver;
  private readonly IServer _server;

  public EnrolmentUrlBuilder(ApiHostOptions hostOptions, ReachableHostResolver hostResolver, IServer server)
  {
    _hostOptions = hostOptions;
    _hostResolver = hostResolver;
    _server = server;
  }

  public string BuildEnrolmentUrl(string qrCodeValue)
  {
    return $"{Origin()}/j/{qrCodeValue}";
  }

  public IReadOnlyList<string> ReachableAddresses()
  {
    return _hostResolver.ReachableAddresses();
  }

  public string Origin()
  {
    return $"http://{_hostResolver.ResolveHost()}:{ResolvePort()}";
  }

  private int ResolvePort()
  {
    if (_hostOptions.Port != 0)
    {
      return _hostOptions.Port;
    }

    var addresses = _server.Features.Get<IServerAddressesFeature>();
    var boundAddress = addresses?.Addresses.FirstOrDefault();

    return boundAddress is not null && Uri.TryCreate(boundAddress, UriKind.Absolute, out var uri)
             ? uri.Port
             : _hostOptions.Port;
  }
}
