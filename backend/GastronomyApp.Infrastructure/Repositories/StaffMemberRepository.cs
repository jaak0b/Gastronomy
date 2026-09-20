using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class StaffMemberRepository : IStaffMemberRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public StaffMemberRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<IReadOnlyList<AdministeredStaffMember>> FindAdministeredAsync(DateTime nowUtc, CancellationToken cancellationToken)
  {
    return await _dbContext.StaffMembers.AsNoTracking()
                           .OrderBy(staffMember => staffMember.Name)
                           .Select(staffMember => new AdministeredStaffMember(staffMember.Id,
                                                                              staffMember.Name,
                                                                              staffMember.IsActive,
                                                                              staffMember.DeviceId != null,
                                                                              _dbContext.Devices.Where(device => device.Id == staffMember.DeviceId).Select(device => (DateTime?)device.LastSeenAtUtc).FirstOrDefault(),
                                                                              _dbContext.EnrolmentInvitations.Any(invitation => invitation.Id == staffMember.EnrolmentInvitationId && invitation.ConsumedAtUtc == null && invitation.ExpiresAtUtc > nowUtc)))
                           .ToListAsync(cancellationToken);
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
