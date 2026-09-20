using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
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

  public async Task<DeviceOwnerRecord?> FindAsync(DeviceOwner owner, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(owner);

    switch (owner.Kind)
    {
      case DeviceOwnerKind.StaffMember:
        var staffMember = await _dbContext.StaffMembers.FirstOrDefaultAsync(candidate => candidate.Id == owner.Id, cancellationToken);

        if (staffMember is null)
          return null;

        return new()
               {
                 Owner = owner,
                 Name = staffMember.Name,
                 IsActive = staffMember.IsActive,
                 DeviceId = staffMember.DeviceId,
                 EnrolmentInvitationId = staffMember.EnrolmentInvitationId
               };
      case DeviceOwnerKind.Station:
        var station = await _dbContext.Stations.FirstOrDefaultAsync(candidate => candidate.Id == owner.Id, cancellationToken);

        if (station is null)
          return null;

        return new()
               {
                 Owner = owner,
                 Name = station.Name,
                 IsActive = station.IsActive,
                 DeviceId = station.DeviceId,
                 EnrolmentInvitationId = station.EnrolmentInvitationId
               };
      default:
        return new UnreachableCase().Throw<DeviceOwnerRecord?>(owner.Kind);
    }
  }

  public async Task<DeviceOwner?> FindByDeviceAsync(Guid deviceId, CancellationToken cancellationToken)
  {
    Guid? staffMemberId = await _dbContext.StaffMembers.Where(staffMember => staffMember.DeviceId == deviceId).Select(staffMember => (Guid?)staffMember.Id).FirstOrDefaultAsync(cancellationToken);

    if (staffMemberId is not null)
      return new(DeviceOwnerKind.StaffMember, staffMemberId.Value);

    Guid? stationId = await _dbContext.Stations.Where(station => station.DeviceId == deviceId).Select(station => (Guid?)station.Id).FirstOrDefaultAsync(cancellationToken);

    if (stationId is null)
      return null;

    return new(DeviceOwnerKind.Station, stationId.Value);
  }

  public async Task<DeviceOwner?> FindByInvitationAsync(Guid invitationId, CancellationToken cancellationToken)
  {
    Guid? staffMemberId = await _dbContext.StaffMembers.Where(staffMember => staffMember.EnrolmentInvitationId == invitationId).Select(staffMember => (Guid?)staffMember.Id).FirstOrDefaultAsync(cancellationToken);

    if (staffMemberId is not null)
      return new(DeviceOwnerKind.StaffMember, staffMemberId.Value);

    Guid? stationId = await _dbContext.Stations.Where(station => station.EnrolmentInvitationId == invitationId).Select(station => (Guid?)station.Id).FirstOrDefaultAsync(cancellationToken);

    if (stationId is null)
      return null;

    return new(DeviceOwnerKind.Station, stationId.Value);
  }

  public async Task PointDeviceAsync(DeviceOwner owner, Guid? deviceId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(owner);

    switch (owner.Kind)
    {
      case DeviceOwnerKind.StaffMember:
        var staffMember = await _dbContext.StaffMembers.SingleAsync(candidate => candidate.Id == owner.Id, cancellationToken);
        staffMember.DeviceId = deviceId;
        return;
      case DeviceOwnerKind.Station:
        var station = await _dbContext.Stations.SingleAsync(candidate => candidate.Id == owner.Id, cancellationToken);
        station.DeviceId = deviceId;
        return;
      default:
        new UnreachableCase().Throw<object>(owner.Kind);
        return;
    }
  }

  public async Task PointInvitationAsync(DeviceOwner owner, Guid? invitationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(owner);

    switch (owner.Kind)
    {
      case DeviceOwnerKind.StaffMember:
        var staffMember = await _dbContext.StaffMembers.SingleAsync(candidate => candidate.Id == owner.Id, cancellationToken);
        staffMember.EnrolmentInvitationId = invitationId;
        return;
      case DeviceOwnerKind.Station:
        var station = await _dbContext.Stations.SingleAsync(candidate => candidate.Id == owner.Id, cancellationToken);
        station.EnrolmentInvitationId = invitationId;
        return;
      default:
        new UnreachableCase().Throw<object>(owner.Kind);
        return;
    }
  }

  public async Task ForgetInvitationsAsync(IReadOnlyCollection<Guid> invitationIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(invitationIds);

    List<Guid> ids = invitationIds.ToList();

    if (ids.Count == 0)
      return;

    List<StaffMember> staffMembers = await _dbContext.StaffMembers.Where(staffMember => staffMember.EnrolmentInvitationId != null && ids.Contains(staffMember.EnrolmentInvitationId.Value)).ToListAsync(cancellationToken);

    foreach (var staffMember in staffMembers)
      staffMember.EnrolmentInvitationId = null;

    List<Station> stations = await _dbContext.Stations.Where(station => station.EnrolmentInvitationId != null && ids.Contains(station.EnrolmentInvitationId.Value)).ToListAsync(cancellationToken);

    foreach (var station in stations)
      station.EnrolmentInvitationId = null;
  }
}
