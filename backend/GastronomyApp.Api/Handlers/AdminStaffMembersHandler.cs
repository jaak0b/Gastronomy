using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Core.Entities;
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

    return await _service.RenameAsync(staffMemberId, request.Name, cancellationToken)
                         .Match(staffMember => Results.Ok(_mapper.Map<StaffMemberView>(staffMember)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> ActivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    return await _service.ActivateAsync(staffMemberId, cancellationToken)
                         .Match(staffMember => Results.Ok(_mapper.Map<StaffMemberView>(staffMember)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> DeactivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    return await _service.DeactivateAsync(staffMemberId, cancellationToken)
                         .Match(staffMember => Results.Ok(_mapper.Map<StaffMemberView>(staffMember)), _resultEnvelope.Refuse);
  }
}
