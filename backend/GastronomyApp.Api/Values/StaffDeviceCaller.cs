using GastronomyApp.Api.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Values;

public sealed record StaffDeviceCaller(Guid StaffMemberId, Guid DeviceId, string Language)
{
  public static ValueTask<StaffDeviceCaller?> BindAsync(HttpContext httpContext)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    return ValueTask.FromResult(httpContext.RequestServices.GetRequiredService<CallerIdentity>().ReadStaffDevice(httpContext.User));
  }
}
