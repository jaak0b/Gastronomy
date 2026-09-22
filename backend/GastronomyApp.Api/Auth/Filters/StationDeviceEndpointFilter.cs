using GastronomyApp.Core.Entities;
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

    return await _gate.FindRefusalAsync<Station>(context.HttpContext) ?? await next(context);
  }
}
