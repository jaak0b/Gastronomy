using System.Security.Claims;

namespace GastronomyApp.Api.Auth;

public sealed record DeviceCaller(Guid ServerPersonId, Guid DeviceId, string Language);

public sealed class CallerIdentity
{
    private readonly DeviceClaimTypes claimTypes = new();

    public DeviceCaller? ReadDevice(ClaimsPrincipal principal)
    {
        string? serverPersonId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        string? deviceId = principal.FindFirstValue(claimTypes.DeviceId);
        string? language = principal.FindFirstValue(claimTypes.Language);

        if (serverPersonId is null || deviceId is null || language is null)
        {
            return null;
        }

        return new DeviceCaller(Guid.Parse(serverPersonId), Guid.Parse(deviceId), language);
    }
}
