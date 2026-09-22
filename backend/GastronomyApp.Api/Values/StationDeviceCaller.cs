using GastronomyApp.Api.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Values;

public sealed record StationDeviceCaller(Guid StationId, Guid DeviceId, string Language)
{
  public static async ValueTask<StationDeviceCaller?> BindAsync(HttpContext httpContext)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    return await httpContext.RequestServices.GetRequiredService<CallerIdentity>().ReadStationDeviceAsync(httpContext.User, httpContext.RequestAborted);
  }
}
