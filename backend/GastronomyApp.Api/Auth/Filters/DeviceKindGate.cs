using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.Enums;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Auth.Filters;

public sealed class DeviceKindGate
{
  private readonly CallerIdentity _callerIdentity;
  private readonly ResultEnvelope _resultEnvelope;

  public DeviceKindGate(CallerIdentity callerIdentity, ResultEnvelope resultEnvelope)
  {
    _callerIdentity = callerIdentity;
    _resultEnvelope = resultEnvelope;
  }

  public IResult? FindRefusal(HttpContext httpContext, DeviceOwnerKind requiredKind)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    var caller = _callerIdentity.ReadDevice(httpContext.User);

    if (caller is not null && caller.OwnerKind == requiredKind)
      return null;

    return _resultEnvelope.Problem(StatusCodes.Status403Forbidden, "WrongDeviceKind", "auth.wrongDeviceKind");
  }
}
