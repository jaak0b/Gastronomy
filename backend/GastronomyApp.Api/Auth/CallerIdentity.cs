using System.Security.Claims;
using GastronomyApp.Api.Values;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Api.Auth;

public sealed class CallerIdentity
{
  private readonly IDeviceOwnerStore _ownerStore;

  public CallerIdentity(IDeviceOwnerStore ownerStore)
  {
    _ownerStore = ownerStore;
  }

  public DeviceCaller? ReadDevice(ClaimsPrincipal principal)
  {
    ArgumentNullException.ThrowIfNull(principal);

    var ownerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    var deviceId = principal.FindFirstValue(Names.DeviceClaims.DeviceId);
    var language = principal.FindFirstValue(Names.DeviceClaims.Language);

    if (ownerId is null || deviceId is null || language is null)
      return null;

    return new(Guid.Parse(ownerId), Guid.Parse(deviceId), language);
  }

  public async Task<IDeviceOwner?> ReadOwnerAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
  {
    var caller = ReadDevice(principal);

    if (caller is null)
      return null;

    return await _ownerStore.FindByDeviceAsync(caller.DeviceId, cancellationToken);
  }

  public async Task<StaffDeviceCaller?> ReadStaffDeviceAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
  {
    var caller = ReadDevice(principal);

    if (caller is null || await _ownerStore.FindByDeviceAsync(caller.DeviceId, cancellationToken) is not StaffMember)
      return null;

    return new(caller.OwnerId, caller.DeviceId, caller.Language);
  }

  public async Task<StationDeviceCaller?> ReadStationDeviceAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
  {
    var caller = ReadDevice(principal);

    if (caller is null || await _ownerStore.FindByDeviceAsync(caller.DeviceId, cancellationToken) is not Station)
      return null;

    return new(caller.OwnerId, caller.DeviceId, caller.Language);
  }
}
