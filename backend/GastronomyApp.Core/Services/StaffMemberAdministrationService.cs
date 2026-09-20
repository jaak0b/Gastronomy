using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class StaffMemberAdministrationService
{
  private readonly IClock _clock;
  private readonly IStaffMemberRepository _repository;
  private readonly DeviceOwnerRetirement _retirement;
  private readonly ITransactionRunner _transactionRunner;

  public StaffMemberAdministrationService(IStaffMemberRepository repository, DeviceOwnerRetirement retirement, ITransactionRunner transactionRunner, IClock clock)
  {
    _repository = repository;
    _retirement = retirement;
    _transactionRunner = transactionRunner;
    _clock = clock;
  }

  public Task<IReadOnlyList<AdministeredStaffMember>> ListAsync(CancellationToken cancellationToken)
  {
    return _repository.FindAdministeredAsync(_clock.UtcNow, cancellationToken);
  }

  public Task<Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>>> RenameAsync(Guid staffMemberId, string? name, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => RenamedAsync(staffMemberId, name, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>>> ActivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOnAsync(staffMemberId, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>>> DeactivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOffAsync(staffMemberId, transactionCancellationToken), cancellationToken);
  }

  private async Task<Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>>> RenamedAsync(Guid staffMemberId, string? name, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(name))
      return Failed(StaffMemberAdministrationFailureReason.NameMissing);

    var staffMember = await _repository.FindByIdAsync(staffMemberId, cancellationToken);

    if (staffMember is null)
      return Failed(StaffMemberAdministrationFailureReason.StaffMemberNotFound);

    staffMember.Name = name;
    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(staffMember, null);
  }

  private async Task<Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>>> SwitchedOnAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    var staffMember = await _repository.FindByIdAsync(staffMemberId, cancellationToken);

    if (staffMember is null)
      return Failed(StaffMemberAdministrationFailureReason.StaffMemberNotFound);

    staffMember.IsActive = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(staffMember, null);
  }

  private async Task<Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>>> SwitchedOffAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    var staffMember = await _repository.FindByIdAsync(staffMemberId, cancellationToken);

    if (staffMember is null)
      return Failed(StaffMemberAdministrationFailureReason.StaffMemberNotFound);

    Guid? deviceId = staffMember.DeviceId;

    staffMember.IsActive = false;
    await _retirement.WithdrawOutstandingInvitationAsync(staffMember.EnrolmentInvitationId, cancellationToken);
    staffMember.EnrolmentInvitationId = null;
    await _repository.SaveChangesAsync(cancellationToken);

    Guid? revokedDeviceId = await _retirement.RevokeDeviceAsync(deviceId, cancellationToken);

    return Saved(staffMember, revokedDeviceId);
  }

  private Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>> Saved(StaffMember staffMember, Guid? revokedDeviceId)
  {
    return Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>>.Success(new(staffMember.Id, staffMember.Name, revokedDeviceId));
  }

  private Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>> Failed(StaffMemberAdministrationFailureReason reason)
  {
    return Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>>.Failed(new() { Reason = reason });
  }

  private async Task<Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>>> RunAsync(Func<CancellationToken, Task<Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>>>> write, CancellationToken cancellationToken)
  {
    return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                             {
                                               Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>> written = await write(transactionCancellationToken);

                                               return new TransactionOutcome<Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>>>
                                                      {
                                                        Value = written,
                                                        ShouldCommit = written.IsSuccess
                                                      };
                                             },
                                             cancellationToken);
  }
}
