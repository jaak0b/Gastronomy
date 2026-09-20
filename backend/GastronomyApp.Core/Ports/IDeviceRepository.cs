using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IDeviceRepository
{
  public Task<Device?> FindByIdAsync(Guid deviceId, CancellationToken cancellationToken);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
