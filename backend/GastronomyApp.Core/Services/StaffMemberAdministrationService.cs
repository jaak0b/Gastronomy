using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class StaffMemberAdministrationService
{
  private readonly IStaffMemberRepository _repository;
  private readonly DeviceOwnerRetirement _retirement;

  public StaffMemberAdministrationService(IStaffMemberRepository repository, DeviceOwnerRetirement retirement)
  {
    _repository = repository;
    _retirement = retirement;
  }

  public Task<IReadOnlyList<StaffMember>> ListAsync(CancellationToken cancellationToken)
  {
    return _repository.FindAllAsync(cancellationToken);
  }

  public async Task<ErrorOr<StaffMember>> RenameAsync(Guid staffMemberId, string? name, CancellationToken cancellationToken)
  {
    var staffMember = await _repository.FindByIdAsync(staffMemberId, cancellationToken);

    if (staffMember is null)
      return Refusal.StaffMember.StaffMemberNotFound(staffMemberId);

    staffMember.Name = name!;
    await _repository.SaveChangesAsync(cancellationToken);

    return staffMember;
  }

  public async Task<ErrorOr<StaffMember>> ActivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    var staffMember = await _repository.FindByIdAsync(staffMemberId, cancellationToken);

    if (staffMember is null)
      return Refusal.StaffMember.StaffMemberNotFound(staffMemberId);

    staffMember.IsActive = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return staffMember;
  }

  public async Task<ErrorOr<StaffMember>> DeactivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    var staffMember = await _repository.FindByIdAsync(staffMemberId, cancellationToken);

    if (staffMember is null)
      return Refusal.StaffMember.StaffMemberNotFound(staffMemberId);

    Guid? deviceId = staffMember.DeviceId;

    staffMember.IsActive = false;
    await _retirement.WithdrawOutstandingInvitationAsync(staffMember.EnrolmentInvitationId, cancellationToken);
    staffMember.EnrolmentInvitationId = null;
    await _repository.SaveChangesAsync(cancellationToken);

    await _retirement.RevokeDeviceAsync(deviceId, cancellationToken);

    return staffMember;
  }
}
