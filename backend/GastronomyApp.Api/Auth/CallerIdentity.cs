using System.Security.Claims;

namespace GastronomyApp.Api.Auth;

public sealed record DeviceCaller(Guid StaffMemberId, Guid DeviceId, string Language);

public sealed class CallerIdentity
{
  private readonly DeviceClaimTypes claimTypes = new();

  public DeviceCaller? ReadDevice(ClaimsPrincipal principal)
  {
    var staffMemberId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    var deviceId = principal.FindFirstValue(claimTypes.DeviceId);
    var language = principal.FindFirstValue(claimTypes.Language);

    if (staffMemberId is null || deviceId is null || language is null)
    {
      return null;
    }

    return new(Guid.Parse(staffMemberId), Guid.Parse(deviceId), language);
  }
}
