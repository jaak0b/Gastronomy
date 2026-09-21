using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class StaffMemberAdministrationService
{
  private readonly IStaffMemberRepository _repository;
  private readonly DeviceOwnerRetirement _retirement;
  private readonly ITransactionRunner _transactionRunner;

  public StaffMemberAdministrationService(IStaffMemberRepository repository, DeviceOwnerRetirement retirement, ITransactionRunner transactionRunner)
  {
    _repository = repository;
    _retirement = retirement;
    _transactionRunner = transactionRunner;
  }

  public Task<IReadOnlyList<StaffMember>> ListAsync(CancellationToken cancellationToken)
  {
    return _repository.FindAllAsync(cancellationToken);
  }

  public Task<Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>>> RenameAsync(Guid staffMemberId, string? name, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => RenamedAsync(staffMemberId, name, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>>> ActivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOnAsync(staffMemberId, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>>> DeactivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOffAsync(staffMemberId, transactionCancellationToken), cancellationToken);
  }

  private async Task<Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>>> RenamedAsync(Guid staffMemberId, string? name, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(name))
      return Failed(StaffMemberAdministrationFailureReason.NameMissing);

    var staffMember = await _repository.FindByIdAsync(staffMemberId, cancellationToken);

    if (staffMember is null)
      return Failed(StaffMemberAdministrationFailureReason.StaffMemberNotFound);

    staffMember.Name = name;
    await _repository.SaveChangesAsync(cancellationToken);

    return Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>>.Success(staffMember);
  }

  private async Task<Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>>> SwitchedOnAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    var staffMember = await _repository.FindByIdAsync(staffMemberId, cancellationToken);

    if (staffMember is null)
      return Failed(StaffMemberAdministrationFailureReason.StaffMemberNotFound);

    staffMember.IsActive = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>>.Success(staffMember);
  }

  private async Task<Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>>> SwitchedOffAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    var staffMember = await _repository.FindByIdAsync(staffMemberId, cancellationToken);

    if (staffMember is null)
      return Failed(StaffMemberAdministrationFailureReason.StaffMemberNotFound);

    Guid? deviceId = staffMember.DeviceId;

    staffMember.IsActive = false;
    await _retirement.WithdrawOutstandingInvitationAsync(staffMember.EnrolmentInvitationId, cancellationToken);
    staffMember.EnrolmentInvitationId = null;
    await _repository.SaveChangesAsync(cancellationToken);

    await _retirement.RevokeDeviceAsync(deviceId, cancellationToken);

    return Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>>.Success(staffMember);
  }

  private Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>> Failed(StaffMemberAdministrationFailureReason reason)
  {
    return Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>>.Failed(new() { Reason = reason });
  }

  private async Task<Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>>> RunAsync(Func<CancellationToken, Task<Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>>>> write, CancellationToken cancellationToken)
  {
    return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                             {
                                               Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>> written = await write(transactionCancellationToken);

                                               return new TransactionOutcome<Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>>>
                                                      {
                                                        Value = written,
                                                        ShouldCommit = written.IsSuccess
                                                      };
                                             },
                                             cancellationToken);
  }
}
