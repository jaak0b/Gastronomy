using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class DeviceOwnerLookup
{
  private readonly IGastronomyDatabase _database;
  private readonly ISecretHasher _secretHasher;

  public DeviceOwnerLookup(IGastronomyDatabase database, ISecretHasher secretHasher)
  {
    _database = database;
    _secretHasher = secretHasher;
  }

  public async Task<StaffMember?> FindStaffMemberAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    return await _database.StaffMembers.Include(staffMember => staffMember.Device).FirstOrDefaultAsync(candidate => candidate.Id == staffMemberId, cancellationToken);
  }

  public async Task<Station?> FindStationAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return await _database.Stations.Include(station => station.Device).FirstOrDefaultAsync(candidate => candidate.Id == stationId, cancellationToken);
  }

  public async Task<IDeviceOwner?> FindByDeviceAsync(Guid deviceId, CancellationToken cancellationToken)
  {
    var staffMember = await _database.StaffMembers.Include(candidate => candidate.Device).FirstOrDefaultAsync(candidate => candidate.DeviceId == deviceId, cancellationToken);

    if (staffMember is not null)
      return staffMember;

    return await _database.Stations.Include(candidate => candidate.Device).FirstOrDefaultAsync(candidate => candidate.DeviceId == deviceId, cancellationToken);
  }

  public async Task<IDeviceOwner?> FindByInvitationAsync(Guid invitationId, CancellationToken cancellationToken)
  {
    var staffMember = await _database.StaffMembers.Include(candidate => candidate.Device).FirstOrDefaultAsync(candidate => candidate.EnrolmentInvitationId == invitationId, cancellationToken);

    if (staffMember is not null)
      return staffMember;

    return await _database.Stations.Include(candidate => candidate.Device).FirstOrDefaultAsync(candidate => candidate.EnrolmentInvitationId == invitationId, cancellationToken);
  }

  public async Task<IDeviceOwner?> FindByTokenAsync(string tokenLookupId, string secret, CancellationToken cancellationToken)
  {
    var device = await _database.Devices.FirstOrDefaultAsync(candidate => candidate.TokenLookupId == tokenLookupId, cancellationToken);

    if (device is null)
      return null;

    var secretMatches = _secretHasher.Verify(secret, device.TokenHash, device.TokenSalt, device.TokenIterations, device.TokenAlgorithm);

    if (!secretMatches)
      return null;

    return await FindByDeviceAsync(device.Id, cancellationToken);
  }
}
