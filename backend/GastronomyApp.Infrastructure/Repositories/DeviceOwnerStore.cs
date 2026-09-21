using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class DeviceOwnerStore : IDeviceOwnerStore
{
  private readonly GastronomyAppDbContext _dbContext;

  public DeviceOwnerStore(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<IDeviceOwner?> FindAsync(DeviceOwnerKind kind, Guid ownerId, CancellationToken cancellationToken)
  {
    switch (kind)
    {
      case DeviceOwnerKind.StaffMember:
        return await _dbContext.StaffMembers.Include(staffMember => staffMember.Device).FirstOrDefaultAsync(candidate => candidate.Id == ownerId, cancellationToken);
      case DeviceOwnerKind.Station:
        return await _dbContext.Stations.Include(station => station.Device).FirstOrDefaultAsync(candidate => candidate.Id == ownerId, cancellationToken);
      default:
        return new UnreachableCase().Throw<IDeviceOwner?>(kind);
    }
  }

  public async Task<IDeviceOwner?> FindByDeviceAsync(Guid deviceId, CancellationToken cancellationToken)
  {
    var staffMember = await _dbContext.StaffMembers.Include(candidate => candidate.Device).FirstOrDefaultAsync(candidate => candidate.DeviceId == deviceId, cancellationToken);

    if (staffMember is not null)
      return staffMember;

    return await _dbContext.Stations.Include(candidate => candidate.Device).FirstOrDefaultAsync(candidate => candidate.DeviceId == deviceId, cancellationToken);
  }

  public async Task<IDeviceOwner?> FindByInvitationAsync(Guid invitationId, CancellationToken cancellationToken)
  {
    var staffMember = await _dbContext.StaffMembers.Include(candidate => candidate.Device).FirstOrDefaultAsync(candidate => candidate.EnrolmentInvitationId == invitationId, cancellationToken);

    if (staffMember is not null)
      return staffMember;

    return await _dbContext.Stations.Include(candidate => candidate.Device).FirstOrDefaultAsync(candidate => candidate.EnrolmentInvitationId == invitationId, cancellationToken);
  }

  public async Task ForgetInvitationsAsync(IReadOnlyCollection<Guid> invitationIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(invitationIds);

    List<Guid> ids = invitationIds.ToList();

    if (ids.Count == 0)
      return;

    List<StaffMember> staffMembers = await _dbContext.StaffMembers.Where(staffMember => staffMember.EnrolmentInvitationId != null && ids.Contains(staffMember.EnrolmentInvitationId.Value)).ToListAsync(cancellationToken);

    foreach (var staffMember in staffMembers)
      ForgetInvitation(staffMember);

    List<Station> stations = await _dbContext.Stations.Where(station => station.EnrolmentInvitationId != null && ids.Contains(station.EnrolmentInvitationId.Value)).ToListAsync(cancellationToken);

    foreach (var station in stations)
      ForgetInvitation(station);
  }

  private void ForgetInvitation(IDeviceOwner owner)
  {
    owner.EnrolmentInvitationId = null;
    owner.EnrolmentInvitation = null;
  }
}
