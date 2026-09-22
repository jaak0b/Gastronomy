using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class SessionService
{
  private readonly IDeviceOwnerStore _ownerStore;

  public SessionService(IDeviceOwnerStore ownerStore)
  {
    _ownerStore = ownerStore;
  }

  public async Task<ErrorOr<IDeviceOwner>> ReadOwnerOfDeviceAsync(Guid deviceId, CancellationToken cancellationToken)
  {
    var owner = await _ownerStore.FindByDeviceAsync(deviceId, cancellationToken);

    if (owner is null)
      return Refusal.Session.OwnerUnknown();

    return owner.ToErrorOr();
  }
}
