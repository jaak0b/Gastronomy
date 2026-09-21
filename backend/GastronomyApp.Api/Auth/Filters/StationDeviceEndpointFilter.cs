using GastronomyApp.Contracts.Enums;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Auth.Filters;

public sealed class StationDeviceEndpointFilter : IEndpointFilter
{
  private readonly DeviceKindGate _gate;

  public StationDeviceEndpointFilter(DeviceKindGate gate)
  {
    _gate = gate;
  }

  public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(next);

    return _gate.FindRefusal(context.HttpContext, DeviceOwnerKind.Station) ?? await next(context);
  }
}
