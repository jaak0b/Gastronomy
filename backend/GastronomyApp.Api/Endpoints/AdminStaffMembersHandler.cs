using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Endpoints;

public sealed class AdminStaffMembersHandler
{
  private readonly DeviceRevocationAnnouncer _revocationAnnouncer;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly StaffMemberAdministrationService _service;

  public AdminStaffMembersHandler(StaffMemberAdministrationService service,
                                  DeviceRevocationAnnouncer revocationAnnouncer,
                                  ResultEnvelope resultEnvelope)
  {
    _service = service;
    _revocationAnnouncer = revocationAnnouncer;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<AdministeredStaffMember> staffMembers = await _service.ListAsync(cancellationToken);

    return Results.Ok(new AdminStaffMemberListView([.. staffMembers.Select(BuildStaffMemberView)]));
  }

  public async Task<IResult> RenameAsync(Guid staffMemberId,
                                         RenameStaffMemberRequest request,
                                         CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<SavedStaffMember, StaffMemberAdministrationFailure> renamed =
      await _service.RenameAsync(staffMemberId, request.Name, cancellationToken);

    return await AnsweredAsync(renamed, cancellationToken);
  }

  public async Task<IResult> ActivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    Result<SavedStaffMember, StaffMemberAdministrationFailure> switchedOn =
      await _service.ActivateAsync(staffMemberId, cancellationToken);

    return await AnsweredAsync(switchedOn, cancellationToken);
  }

  public async Task<IResult> DeactivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    Result<SavedStaffMember, StaffMemberAdministrationFailure> switchedOff =
      await _service.DeactivateAsync(staffMemberId, cancellationToken);

    return await AnsweredAsync(switchedOff, cancellationToken);
  }

  private AdminStaffMemberView BuildStaffMemberView(AdministeredStaffMember staffMember)
  {
    return new(staffMember.StaffMemberId,
               staffMember.Name,
               staffMember.IsActive,
               staffMember.HasDevice,
               staffMember.LastSeenAtUtc,
               staffMember.HasOutstandingInvitation);
  }

  private async Task<IResult> AnsweredAsync(Result<SavedStaffMember, StaffMemberAdministrationFailure> written,
                                            CancellationToken cancellationToken)
  {
    if (!written.IsSuccess)
    {
      return RefusalFor(written.Failure);
    }

    await _revocationAnnouncer.AnnounceAsync(written.Value.RevokedDeviceId, cancellationToken);

    return Results.Ok(new StaffMemberView(written.Value.StaffMemberId, written.Value.Name));
  }

  private IResult RefusalFor(StaffMemberAdministrationFailure failure)
  {
    return failure.Reason switch
           {
             StaffMemberAdministrationFailureReason.StaffMemberNotFound => Results.NotFound(),
             StaffMemberAdministrationFailureReason.NameMissing =>
               _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                       "ValidationFailed",
                                       "admin.staff.nameMissing"),
             _ => new UnreachableCase().Throw<IResult>(failure.Reason)
           };
  }
}
