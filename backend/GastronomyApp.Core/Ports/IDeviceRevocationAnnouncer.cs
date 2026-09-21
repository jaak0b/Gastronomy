namespace GastronomyApp.Core.Ports;

public interface IDeviceRevocationAnnouncer
{
  public Task AnnounceDeviceRevokedAsync(Guid revokedDeviceId, CancellationToken cancellationToken);
}
