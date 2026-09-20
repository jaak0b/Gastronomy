using GastronomyApp.Core.Entities;
using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Ports;

public interface IStaffMemberRepository
{
  public Task<IReadOnlyList<AdministeredStaffMember>> FindAdministeredAsync(DateTime nowUtc,
                                                                            CancellationToken cancellationToken);

  public Task<StaffMember?> FindByIdAsync(Guid staffMemberId, CancellationToken cancellationToken);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
