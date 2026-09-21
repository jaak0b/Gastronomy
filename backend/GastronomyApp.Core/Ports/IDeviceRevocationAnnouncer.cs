namespace GastronomyApp.Core.Ports;

public interface IDeviceRevocationAnnouncer
{
  public Task AnnounceAsync(Guid revokedDeviceId, CancellationToken cancellationToken);
}
