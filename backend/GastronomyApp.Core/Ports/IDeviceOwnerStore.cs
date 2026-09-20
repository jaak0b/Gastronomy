using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Ports;

public interface IDeviceOwnerStore
{
  public Task<DeviceOwnerRecord?> FindAsync(DeviceOwner owner, CancellationToken cancellationToken);

  public Task<DeviceOwner?> FindByDeviceAsync(Guid deviceId, CancellationToken cancellationToken);

  public Task<DeviceOwner?> FindByInvitationAsync(Guid invitationId, CancellationToken cancellationToken);

  public Task PointDeviceAsync(DeviceOwner owner, Guid? deviceId, CancellationToken cancellationToken);

  public Task PointInvitationAsync(DeviceOwner owner, Guid? invitationId, CancellationToken cancellationToken);

  public Task ForgetInvitationsAsync(IReadOnlyCollection<Guid> invitationIds, CancellationToken cancellationToken);
}
