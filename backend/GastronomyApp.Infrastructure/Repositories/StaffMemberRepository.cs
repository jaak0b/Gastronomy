using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.QueryRows;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class StaffMemberRepository : IStaffMemberRepository
{
  private readonly GastronomyAppDbContext _dbContext;
  private readonly TypeAdapterConfig _mapperConfig;

  public StaffMemberRepository(GastronomyAppDbContext dbContext, TypeAdapterConfig mapperConfig)
  {
    _dbContext = dbContext;
    _mapperConfig = mapperConfig;
  }

  public async Task<IReadOnlyList<AdministeredStaffMember>> FindAdministeredAsync(DateTime nowUtc, CancellationToken cancellationToken)
  {
    return await _dbContext.StaffMembers.AsNoTracking()
                           .OrderBy(staffMember => staffMember.Name)
                           .Select(staffMember => new AdministeredStaffMemberRow
                                                  {
                                                    StaffMember = staffMember,
                                                    LastSeenAtUtc
                                                      = _dbContext.Devices.Where(device => device.Id == staffMember.DeviceId).Select(device => (DateTime?)device.LastSeenAtUtc).FirstOrDefault(),
                                                    HasOutstandingInvitation
                                                      = _dbContext.EnrolmentInvitations.Any(invitation => invitation.Id == staffMember.EnrolmentInvitationId
                                                                                                          && invitation.ConsumedAtUtc == null
                                                                                                          && invitation.ExpiresAtUtc > nowUtc)
                                                  })
                           .ProjectToType<AdministeredStaffMember>(_mapperConfig)
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
