using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminStaffMembersHandler
{
  private readonly IMapper _mapper;
  private readonly StaffMemberAdministrationService _service;

  public AdminStaffMembersHandler(StaffMemberAdministrationService service, IMapper mapper)
  {
    _service = service;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<AdminStaffMemberListView>> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<StaffMember> staffMembers = await _service.ListAsync(cancellationToken);

    return new AdminStaffMemberListView(_mapper.Map<IReadOnlyList<AdminStaffMemberView>>(staffMembers));
  }

  public async Task<ApiAnswer<StaffMemberView>> RenameAsync(Guid staffMemberId, RenameStaffMemberRequest request, CancellationToken cancellationToken)
  {
    return await _service.RenameAsync(staffMemberId, request.Name, cancellationToken).Then(_mapper.Map<StaffMemberView>);
  }

  public async Task<ApiAnswer<StaffMemberView>> ActivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    return await _service.ActivateAsync(staffMemberId, cancellationToken).Then(_mapper.Map<StaffMemberView>);
  }

  public async Task<ApiAnswer<StaffMemberView>> DeactivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    return await _service.DeactivateAsync(staffMemberId, cancellationToken).Then(_mapper.Map<StaffMemberView>);
  }
}
