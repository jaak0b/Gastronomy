using System.Security.Claims;
using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Auth;

public sealed record DeviceCaller(DeviceOwnerKind OwnerKind, Guid OwnerId, Guid DeviceId, string Language);

public sealed record StaffDeviceCaller(Guid StaffMemberId, Guid DeviceId, string Language);

public sealed record StationDeviceCaller(Guid StationId, Guid DeviceId, string Language);

public sealed class CallerIdentity
{
  private readonly DeviceClaimTypes _claimTypes = new();

  public DeviceCaller? ReadDevice(ClaimsPrincipal principal)
  {
    ArgumentNullException.ThrowIfNull(principal);

    var ownerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    var ownerKind = principal.FindFirstValue(_claimTypes.OwnerKind);
    var deviceId = principal.FindFirstValue(_claimTypes.DeviceId);
    var language = principal.FindFirstValue(_claimTypes.Language);

    if (ownerId is null
        || deviceId is null
        || language is null
        || !Enum.TryParse(ownerKind, out DeviceOwnerKind parsedOwnerKind))
    {
      return null;
    }

    return new(parsedOwnerKind, Guid.Parse(ownerId), Guid.Parse(deviceId), language);
  }

  public StaffDeviceCaller? ReadStaffDevice(ClaimsPrincipal principal)
  {
    var caller = ReadDevice(principal);

    return caller is null || caller.OwnerKind != DeviceOwnerKind.StaffMember
             ? null
             : new(caller.OwnerId, caller.DeviceId, caller.Language);
  }

  public StationDeviceCaller? ReadStationDevice(ClaimsPrincipal principal)
  {
    var caller = ReadDevice(principal);

    return caller is null || caller.OwnerKind != DeviceOwnerKind.Station
             ? null
             : new(caller.OwnerId, caller.DeviceId, caller.Language);
  }
}
