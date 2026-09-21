using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminStaffMembersHandler
{
  private readonly IMapper _mapper;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly StaffMemberAdministrationService _service;

  public AdminStaffMembersHandler(StaffMemberAdministrationService service, ResultEnvelope resultEnvelope, IMapper mapper)
  {
    _service = service;
    _resultEnvelope = resultEnvelope;
    _mapper = mapper;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<StaffMember> staffMembers = await _service.ListAsync(cancellationToken);

    return Results.Ok(new AdminStaffMemberListView(_mapper.Map<IReadOnlyList<AdminStaffMemberView>>(staffMembers)));
  }

  public async Task<IResult> RenameAsync(Guid staffMemberId, RenameStaffMemberRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>> renamed = await _service.RenameAsync(staffMemberId, request.Name, cancellationToken);

    return Answered(renamed);
  }

  public async Task<IResult> ActivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>> switchedOn = await _service.ActivateAsync(staffMemberId, cancellationToken);

    return Answered(switchedOn);
  }

  public async Task<IResult> DeactivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>> switchedOff = await _service.DeactivateAsync(staffMemberId, cancellationToken);

    return Answered(switchedOff);
  }

  private IResult Answered(Result<StaffMember, Failure<StaffMemberAdministrationFailureReason>> written)
  {
    if (!written.IsSuccess)
      return RefusalFor(written.Failure);

    return Results.Ok(_mapper.Map<StaffMemberView>(written.Value));
  }

  private IResult RefusalFor(Failure<StaffMemberAdministrationFailureReason> failure)
  {
    return failure.Reason switch
           {
             StaffMemberAdministrationFailureReason.StaffMemberNotFound => Results.NotFound(),
             _ => new UnreachableCase().Throw<IResult>(failure.Reason)
           };
  }
}
