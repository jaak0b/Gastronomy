using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class StaffMemberRepository : IStaffMemberRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public StaffMemberRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<IReadOnlyList<StaffMember>> FindAllAsync(CancellationToken cancellationToken)
  {
    return await _dbContext.StaffMembers.AsNoTracking().OrderBy(staffMember => staffMember.Name).ToListAsync(cancellationToken);
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
