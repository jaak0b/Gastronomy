using ErrorOr;
using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class StaffMemberService
{
  private readonly IGastronomyDatabase _database;
  private readonly TypeAdapterConfig _mapping;
  private readonly DeviceOwnerRetirement _retirement;

  public StaffMemberService(IGastronomyDatabase database, TypeAdapterConfig mapping, DeviceOwnerRetirement retirement)
  {
    _database = database;
    _mapping = mapping;
    _retirement = retirement;
  }

  public async Task<AdminStaffMemberListView> ListAsync(CancellationToken cancellationToken)
  {
    var staffMembers = await _database.StaffMembers.AsNoTracking().OrderBy(staffMember => staffMember.Name).ProjectToType<AdminStaffMemberView>(_mapping).ToListAsync(cancellationToken);

    return new(staffMembers);
  }

  public async Task<ErrorOr<StaffMemberView>> RenameAsync(Guid staffMemberId, RenameStaffMemberRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var staffMember = await _database.StaffMembers.FirstOrDefaultAsync(row => row.Id == staffMemberId, cancellationToken);

    if (staffMember is null)
      return Refusal.StaffMember.StaffMemberNotFound(staffMemberId);

    staffMember.Name = request.Name!;
    await _database.SaveChangesAsync(cancellationToken);

    return staffMember.Adapt<StaffMemberView>(_mapping);
  }

  public async Task<ErrorOr<StaffMemberView>> DeactivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    var staffMember = await _database.StaffMembers.FirstOrDefaultAsync(row => row.Id == staffMemberId, cancellationToken);

    if (staffMember is null)
      return Refusal.StaffMember.StaffMemberNotFound(staffMemberId);

    var deviceId = staffMember.DeviceId;

    staffMember.IsActive = false;
    await _retirement.WithdrawOutstandingInvitationAsync(staffMember.EnrolmentInvitationId, cancellationToken);
    staffMember.EnrolmentInvitationId = null;
    await _database.SaveChangesAsync(cancellationToken);

    await _retirement.RevokeDeviceAsync(deviceId, cancellationToken);

    return staffMember.Adapt<StaffMemberView>(_mapping);
  }
}
