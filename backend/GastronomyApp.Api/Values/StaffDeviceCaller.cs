using GastronomyApp.Api.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Values;

public sealed record StaffDeviceCaller(Guid StaffMemberId, Guid DeviceId, string Language)
{
  public static async ValueTask<StaffDeviceCaller?> BindAsync(HttpContext httpContext)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    return await httpContext.RequestServices.GetRequiredService<CallerIdentity>().ReadStaffDeviceAsync(httpContext.User, httpContext.RequestAborted);
  }
}
