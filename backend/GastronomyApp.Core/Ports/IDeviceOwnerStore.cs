using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IDeviceOwnerStore
{
  public Task<StaffMember?> FindStaffMemberAsync(Guid staffMemberId, CancellationToken cancellationToken);

  public Task<Station?> FindStationAsync(Guid stationId, CancellationToken cancellationToken);

  public Task<IDeviceOwner?> FindByDeviceAsync(Guid deviceId, CancellationToken cancellationToken);

  public Task<IDeviceOwner?> FindByInvitationAsync(Guid invitationId, CancellationToken cancellationToken);

  public Task ForgetInvitationsAsync(IReadOnlyCollection<Guid> invitationIds, CancellationToken cancellationToken);
}
