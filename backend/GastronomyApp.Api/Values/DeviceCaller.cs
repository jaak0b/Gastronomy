using GastronomyApp.Api.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Values;

public sealed record DeviceCaller(Guid OwnerId, Guid DeviceId, string Language)
{
  public static ValueTask<DeviceCaller?> BindAsync(HttpContext httpContext)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    return ValueTask.FromResult(httpContext.RequestServices.GetRequiredService<CallerIdentity>().ReadDevice(httpContext.User));
  }
}
