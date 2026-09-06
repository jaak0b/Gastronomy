using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed record DeviceOwnerRecord
{
  public required DeviceOwner Owner { get; init; }

  public required string Name { get; init; }

  public required bool IsActive { get; init; }

  public required Guid? DeviceId { get; init; }

  public required Guid? EnrolmentInvitationId { get; init; }
}

public sealed class DeviceOwnerStore
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
        var staffMember = await _dbContext.StaffMembers
                                          .FirstOrDefaultAsync(candidate => candidate.Id == owner.Id, cancellationToken);

        return staffMember is null
                 ? null
                 : new()
                   {
                     Owner = owner,
                     Name = staffMember.Name,
                     IsActive = staffMember.IsActive,
                     DeviceId = staffMember.DeviceId,
                     EnrolmentInvitationId = staffMember.EnrolmentInvitationId
                   };
      case DeviceOwnerKind.Station:
        var station = await _dbContext.Stations
                                      .FirstOrDefaultAsync(candidate => candidate.Id == owner.Id, cancellationToken);

        return station is null
                 ? null
                 : new()
                   {
                     Owner = owner,
                     Name = station.Name,
                     IsActive = station.IsActive,
                     DeviceId = station.DeviceId,
                     EnrolmentInvitationId = station.EnrolmentInvitationId
                   };
      default:
        return new Never().OfType<DeviceOwnerRecord?>(owner.Kind);
    }
  }

  public async Task<DeviceOwner?> FindByDeviceAsync(Guid deviceId, CancellationToken cancellationToken)
  {
    var staffMemberId = await _dbContext.StaffMembers
                                        .Where(staffMember => staffMember.DeviceId == deviceId)
                                        .Select(staffMember => (Guid?)staffMember.Id)
                                        .FirstOrDefaultAsync(cancellationToken);

    if (staffMemberId is not null)
    {
      return new(DeviceOwnerKind.StaffMember, staffMemberId.Value);
    }

    var stationId = await _dbContext.Stations
                                    .Where(station => station.DeviceId == deviceId)
                                    .Select(station => (Guid?)station.Id)
                                    .FirstOrDefaultAsync(cancellationToken);

    return stationId is null ? null : new(DeviceOwnerKind.Station, stationId.Value);
  }

  public async Task<DeviceOwner?> FindByInvitationAsync(Guid invitationId, CancellationToken cancellationToken)
  {
    var staffMemberId = await _dbContext.StaffMembers
                                        .Where(staffMember => staffMember.EnrolmentInvitationId == invitationId)
                                        .Select(staffMember => (Guid?)staffMember.Id)
                                        .FirstOrDefaultAsync(cancellationToken);

    if (staffMemberId is not null)
    {
      return new(DeviceOwnerKind.StaffMember, staffMemberId.Value);
    }

    var stationId = await _dbContext.Stations
                                    .Where(station => station.EnrolmentInvitationId == invitationId)
                                    .Select(station => (Guid?)station.Id)
                                    .FirstOrDefaultAsync(cancellationToken);

    return stationId is null ? null : new(DeviceOwnerKind.Station, stationId.Value);
  }

  public async Task PointDeviceAsync(DeviceOwner owner, Guid? deviceId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(owner);

    switch (owner.Kind)
    {
      case DeviceOwnerKind.StaffMember:
        var staffMember = await _dbContext.StaffMembers
                                          .SingleAsync(candidate => candidate.Id == owner.Id, cancellationToken);
        staffMember.DeviceId = deviceId;
        return;
      case DeviceOwnerKind.Station:
        var station = await _dbContext.Stations
                                      .SingleAsync(candidate => candidate.Id == owner.Id, cancellationToken);
        station.DeviceId = deviceId;
        return;
      default:
        new Never().OfType<object>(owner.Kind);
        return;
    }
  }

  public async Task PointInvitationAsync(DeviceOwner owner, Guid? invitationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(owner);

    switch (owner.Kind)
    {
      case DeviceOwnerKind.StaffMember:
        var staffMember = await _dbContext.StaffMembers
                                          .SingleAsync(candidate => candidate.Id == owner.Id, cancellationToken);
        staffMember.EnrolmentInvitationId = invitationId;
        return;
      case DeviceOwnerKind.Station:
        var station = await _dbContext.Stations
                                      .SingleAsync(candidate => candidate.Id == owner.Id, cancellationToken);
        station.EnrolmentInvitationId = invitationId;
        return;
      default:
        new Never().OfType<object>(owner.Kind);
        return;
    }
  }

  public async Task ForgetInvitationsAsync(IReadOnlyCollection<Guid> invitationIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(invitationIds);

    List<Guid> ids = [.. invitationIds];

    if (ids.Count == 0)
    {
      return;
    }

    var staffMembers = await _dbContext.StaffMembers
                                       .Where(staffMember => staffMember.EnrolmentInvitationId != null
                                                             && ids.Contains(staffMember.EnrolmentInvitationId.Value))
                                       .ToListAsync(cancellationToken);

    foreach (var staffMember in staffMembers)
    {
      staffMember.EnrolmentInvitationId = null;
    }

    var stations = await _dbContext.Stations
                                   .Where(station => station.EnrolmentInvitationId != null
                                                     && ids.Contains(station.EnrolmentInvitationId.Value))
                                   .ToListAsync(cancellationToken);

    foreach (var station in stations)
    {
      station.EnrolmentInvitationId = null;
    }
  }
}
