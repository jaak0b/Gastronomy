using Microsoft.AspNetCore.Authentication;

namespace GastronomyApp.Api.Auth;

public sealed class DeviceAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
}

public sealed record DeviceClaimTypes
{
  public string DeviceId { get; } = "device_id";
  public string Language { get; } = "language";
}

public sealed record AuthenticationSchemeNames
{
  public string Device { get; } = "Device";
}
