using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminStaffMembersHandler
{
  private readonly IMapper _mapper;
  private readonly DeviceRevocationAnnouncer _revocationAnnouncer;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly StaffMemberAdministrationService _service;

  public AdminStaffMembersHandler(StaffMemberAdministrationService service, DeviceRevocationAnnouncer revocationAnnouncer, ResultEnvelope resultEnvelope, IMapper mapper)
  {
    _service = service;
    _revocationAnnouncer = revocationAnnouncer;
    _resultEnvelope = resultEnvelope;
    _mapper = mapper;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<AdministeredStaffMember> staffMembers = await _service.ListAsync(cancellationToken);

    return Results.Ok(new AdminStaffMemberListView(_mapper.Map<IReadOnlyList<AdminStaffMemberView>>(staffMembers)));
  }

  public async Task<IResult> RenameAsync(Guid staffMemberId, RenameStaffMemberRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>> renamed = await _service.RenameAsync(staffMemberId, request.Name, cancellationToken);

    return await AnsweredAsync(renamed, cancellationToken);
  }

  public async Task<IResult> ActivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>> switchedOn = await _service.ActivateAsync(staffMemberId, cancellationToken);

    return await AnsweredAsync(switchedOn, cancellationToken);
  }

  public async Task<IResult> DeactivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>> switchedOff = await _service.DeactivateAsync(staffMemberId, cancellationToken);

    return await AnsweredAsync(switchedOff, cancellationToken);
  }

  private async Task<IResult> AnsweredAsync(Result<SavedStaffMember, Failure<StaffMemberAdministrationFailureReason>> written, CancellationToken cancellationToken)
  {
    if (!written.IsSuccess)
      return RefusalFor(written.Failure);

    await _revocationAnnouncer.AnnounceAsync(written.Value.RevokedDeviceId, cancellationToken);

    return Results.Ok(new StaffMemberView(written.Value.StaffMemberId, written.Value.Name));
  }

  private IResult RefusalFor(Failure<StaffMemberAdministrationFailureReason> failure)
  {
    return failure.Reason switch
           {
             StaffMemberAdministrationFailureReason.StaffMemberNotFound => Results.NotFound(),
             StaffMemberAdministrationFailureReason.NameMissing => _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "admin.staff.nameMissing"),
             _ => new UnreachableCase().Throw<IResult>(failure.Reason)
           };
  }
}
