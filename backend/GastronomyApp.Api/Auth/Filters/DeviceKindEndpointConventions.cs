using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Auth.Filters;

public static class DeviceKindEndpointConventions
{
  public static TBuilder RequireStaffDevice<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
  {
    return builder.AddEndpointFilter<TBuilder, StaffDeviceEndpointFilter>();
  }

  public static TBuilder RequireStationDevice<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
  {
    return builder.AddEndpointFilter<TBuilder, StationDeviceEndpointFilter>();
  }
}
