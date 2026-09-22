using GastronomyApp.Api.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Values;

public sealed record StationDeviceCaller(Guid StationId, Guid DeviceId, string Language)
{
  public static ValueTask<StationDeviceCaller?> BindAsync(HttpContext httpContext)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    return ValueTask.FromResult(httpContext.RequestServices.GetRequiredService<CallerIdentity>().ReadStationDevice(httpContext.User));
  }
}
