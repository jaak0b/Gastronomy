using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Auth.Filters;

public sealed class DeviceKindGate
{
  private readonly ResultEnvelope _resultEnvelope;

  public DeviceKindGate(ResultEnvelope resultEnvelope)
  {
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult?> FindRefusalAsync<TOwner>(HttpContext httpContext) where TOwner : IDeviceOwner
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    var callerIdentity = httpContext.RequestServices.GetRequiredService<CallerIdentity>();
    var owner = await callerIdentity.ReadOwnerAsync(httpContext.User, httpContext.RequestAborted);

    if (owner is TOwner)
      return null;

    return _resultEnvelope.Problem(StatusCodes.Status403Forbidden, "WrongDeviceKind", "auth.wrongDeviceKind");
  }
}
