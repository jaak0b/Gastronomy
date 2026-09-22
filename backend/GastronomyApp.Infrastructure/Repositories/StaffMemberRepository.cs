using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class StaffMemberRepository : IStaffMemberRepository
{
  private readonly TimeProvider _timeProvider;
  private readonly GastronomyAppDbContext _dbContext;
  private readonly EnrolmentInvitationRules _invitationRules;

  public StaffMemberRepository(GastronomyAppDbContext dbContext, TimeProvider timeProvider, EnrolmentInvitationRules invitationRules)
  {
    _dbContext = dbContext;
    _timeProvider = timeProvider;
    _invitationRules = invitationRules;
  }

  public async Task<IReadOnlyList<StaffMember>> FindAllAsync(CancellationToken cancellationToken)
  {
    List<StaffMember> staffMembers = await _dbContext.StaffMembers.AsNoTracking().OrderBy(staffMember => staffMember.Name).Include(staffMember => staffMember.Device).Include(staffMember => staffMember.EnrolmentInvitation).ToListAsync(cancellationToken);

    var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

    foreach (var staffMember in staffMembers.Where(staffMember => staffMember.EnrolmentInvitation != null && !_invitationRules.IsOutstandingAt(staffMember.EnrolmentInvitation, nowUtc)))
      staffMember.EnrolmentInvitation = null;

    return staffMembers;
  }

  public async Task<StaffMember?> FindByIdAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    return await _dbContext.StaffMembers.FirstOrDefaultAsync(staffMember => staffMember.Id == staffMemberId, cancellationToken);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
