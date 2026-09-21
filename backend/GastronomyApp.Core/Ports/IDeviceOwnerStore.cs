using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IDeviceOwnerStore
{
  public Task<IDeviceOwner?> FindAsync(DeviceOwnerKind kind, Guid ownerId, CancellationToken cancellationToken);

  public Task<IDeviceOwner?> FindByDeviceAsync(Guid deviceId, CancellationToken cancellationToken);

  public Task<IDeviceOwner?> FindByInvitationAsync(Guid invitationId, CancellationToken cancellationToken);

  public Task ForgetInvitationsAsync(IReadOnlyCollection<Guid> invitationIds, CancellationToken cancellationToken);
}
