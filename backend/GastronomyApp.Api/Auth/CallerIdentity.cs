using System.Security.Claims;

namespace GastronomyApp.Api.Auth;

public sealed record DeviceCaller(Guid StaffMemberId, Guid DeviceId, string Language);

public sealed class CallerIdentity
{
    private readonly DeviceClaimTypes claimTypes = new();

    public DeviceCaller? ReadDevice(ClaimsPrincipal principal)
    {
        string? staffMemberId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        string? deviceId = principal.FindFirstValue(claimTypes.DeviceId);
        string? language = principal.FindFirstValue(claimTypes.Language);

        if (staffMemberId is null || deviceId is null || language is null)
        {
            return null;
        }

        return new DeviceCaller(Guid.Parse(staffMemberId), Guid.Parse(deviceId), language);
    }
}
