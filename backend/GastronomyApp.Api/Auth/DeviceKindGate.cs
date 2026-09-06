using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Auth;

public sealed class DeviceKindGate
{
  private readonly CallerIdentity _callerIdentity;
  private readonly ResultEnvelope _resultEnvelope;

  public DeviceKindGate(CallerIdentity callerIdentity, ResultEnvelope resultEnvelope)
  {
    _callerIdentity = callerIdentity;
    _resultEnvelope = resultEnvelope;
  }

  public IResult? RefusalFor(HttpContext httpContext, DeviceOwnerKind requiredKind)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    var caller = _callerIdentity.ReadDevice(httpContext.User);

    return caller is not null && caller.OwnerKind == requiredKind
             ? null
             : _resultEnvelope.Problem(StatusCodes.Status403Forbidden,
                                      "WrongDeviceKind",
                                      "auth.wrongDeviceKind");
  }
}

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

                                                           return gate.RefusalFor(invocationContext.HttpContext, requiredKind)
                                                                  ?? await next(invocationContext);
                                                         });
  }
}
