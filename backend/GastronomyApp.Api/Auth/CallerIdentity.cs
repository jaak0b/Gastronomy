using System.Security.Claims;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Api.Auth;

public sealed class CallerIdentity
{
  public DeviceCaller? ReadDevice(ClaimsPrincipal principal)
  {
    ArgumentNullException.ThrowIfNull(principal);

    var ownerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    var ownerKind = principal.FindFirstValue(Names.DeviceClaims.OwnerKind);
    var deviceId = principal.FindFirstValue(Names.DeviceClaims.DeviceId);
    var language = principal.FindFirstValue(Names.DeviceClaims.Language);

    if (ownerId is null || deviceId is null || language is null || !Enum.TryParse(ownerKind, out DeviceOwnerKind parsedOwnerKind))
      return null;

    return new(parsedOwnerKind, Guid.Parse(ownerId), Guid.Parse(deviceId), language);
  }

  public StaffDeviceCaller? ReadStaffDevice(ClaimsPrincipal principal)
  {
    var caller = ReadDevice(principal);

    if (caller is null || caller.OwnerKind != DeviceOwnerKind.StaffMember)
      return null;

    return new(caller.OwnerId, caller.DeviceId, caller.Language);
  }

  public StationDeviceCaller? ReadStationDevice(ClaimsPrincipal principal)
  {
    var caller = ReadDevice(principal);

    if (caller is null || caller.OwnerKind != DeviceOwnerKind.Station)
      return null;

    return new(caller.OwnerId, caller.DeviceId, caller.Language);
  }
}
