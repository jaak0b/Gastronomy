using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IStaffMemberRepository
{
  public Task<IReadOnlyList<StaffMember>> FindAllAsync(CancellationToken cancellationToken);

  public Task<StaffMember?> FindByIdAsync(Guid staffMemberId, CancellationToken cancellationToken);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
