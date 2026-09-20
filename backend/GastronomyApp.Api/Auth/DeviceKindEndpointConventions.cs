using GastronomyApp.Core.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Auth;

public static class DeviceKindEndpointConventions
{
  public static TBuilder RequireStaffDevice<TBuilder>(this TBuilder builder)
    where TBuilder : IEndpointConventionBuilder
  {
    return RequireDeviceKind(builder, DeviceOwnerKind.StaffMember);
  }

  public static TBuilder RequireStationDevice<TBuilder>(this TBuilder builder)
    where TBuilder : IEndpointConventionBuilder
  {
    return RequireDeviceKind(builder, DeviceOwnerKind.Station);
  }

  private static TBuilder RequireDeviceKind<TBuilder>(TBuilder builder, DeviceOwnerKind requiredKind)
    where TBuilder : IEndpointConventionBuilder
  {
    return builder.AddEndpointFilterFactory((_, next) => async invocationContext =>
                                                         {
                                                           var gate = invocationContext.HttpContext
                                                                                       .RequestServices
                                                                                       .GetRequiredService<DeviceKindGate>();

                                                           return gate.FindRefusal(invocationContext.HttpContext, requiredKind)
                                                                  ?? await next(invocationContext);
                                                         });
  }
}
