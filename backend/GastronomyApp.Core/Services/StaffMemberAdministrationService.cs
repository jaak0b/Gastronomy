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

  public StaffMemberAdministrationService(IStaffMemberRepository repository,
                                          DeviceOwnerRetirement retirement,
                                          ITransactionRunner transactionRunner,
                                          IClock clock)
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

  public Task<Result<SavedStaffMember, StaffMemberAdministrationFailure>> RenameAsync(
    Guid staffMemberId,
    string? name,
    CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => RenamedAsync(staffMemberId,
                                                                 name,
                                                                 transactionCancellationToken),
                    cancellationToken);
  }

  public Task<Result<SavedStaffMember, StaffMemberAdministrationFailure>> ActivateAsync(
    Guid staffMemberId,
    CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOnAsync(staffMemberId, transactionCancellationToken),
                    cancellationToken);
  }

  public Task<Result<SavedStaffMember, StaffMemberAdministrationFailure>> DeactivateAsync(
    Guid staffMemberId,
    CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOffAsync(staffMemberId, transactionCancellationToken),
                    cancellationToken);
  }

  private async Task<Result<SavedStaffMember, StaffMemberAdministrationFailure>> RenamedAsync(
    Guid staffMemberId,
    string? name,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return Failed(StaffMemberAdministrationFailureReason.NameMissing);
    }

    StaffMember? staffMember = await _repository.FindByIdAsync(staffMemberId, cancellationToken);

    if (staffMember is null)
    {
      return Failed(StaffMemberAdministrationFailureReason.StaffMemberNotFound);
    }

    staffMember.Name = name;
    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(staffMember, null);
  }

  private async Task<Result<SavedStaffMember, StaffMemberAdministrationFailure>> SwitchedOnAsync(
    Guid staffMemberId,
    CancellationToken cancellationToken)
  {
    StaffMember? staffMember = await _repository.FindByIdAsync(staffMemberId, cancellationToken);

    if (staffMember is null)
    {
      return Failed(StaffMemberAdministrationFailureReason.StaffMemberNotFound);
    }

    staffMember.IsActive = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(staffMember, null);
  }

  private async Task<Result<SavedStaffMember, StaffMemberAdministrationFailure>> SwitchedOffAsync(
    Guid staffMemberId,
    CancellationToken cancellationToken)
  {
    StaffMember? staffMember = await _repository.FindByIdAsync(staffMemberId, cancellationToken);

    if (staffMember is null)
    {
      return Failed(StaffMemberAdministrationFailureReason.StaffMemberNotFound);
    }

    var deviceId = staffMember.DeviceId;

    staffMember.IsActive = false;
    await _retirement.WithdrawOutstandingInvitationAsync(staffMember.EnrolmentInvitationId, cancellationToken);
    staffMember.EnrolmentInvitationId = null;
    await _repository.SaveChangesAsync(cancellationToken);

    var revokedDeviceId = await _retirement.RevokeDeviceAsync(deviceId, cancellationToken);

    return Saved(staffMember, revokedDeviceId);
  }

  private Result<SavedStaffMember, StaffMemberAdministrationFailure> Saved(StaffMember staffMember,
                                                                            Guid? revokedDeviceId)
  {
    return Result<SavedStaffMember, StaffMemberAdministrationFailure>.Success(new(staffMember.Id,
                                                                                  staffMember.Name,
                                                                                  revokedDeviceId));
  }

  private Result<SavedStaffMember, StaffMemberAdministrationFailure> Failed(
    StaffMemberAdministrationFailureReason reason)
  {
    return Result<SavedStaffMember, StaffMemberAdministrationFailure>.Failed(new() { Reason = reason });
  }

  private async Task<Result<SavedStaffMember, StaffMemberAdministrationFailure>> RunAsync(
    Func<CancellationToken, Task<Result<SavedStaffMember, StaffMemberAdministrationFailure>>> write,
    CancellationToken cancellationToken)
  {
    return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                             {
                                               Result<SavedStaffMember, StaffMemberAdministrationFailure> written =
                                                 await write(transactionCancellationToken);

                                               return new TransactionOutcome<Result<SavedStaffMember, StaffMemberAdministrationFailure>>
                                                      {
                                                        Value = written,
                                                        ShouldCommit = written.IsSuccess
                                                      };
                                             },
                                             cancellationToken);
  }
}
