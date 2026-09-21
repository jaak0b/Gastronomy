namespace GastronomyApp.Api.Hub;

public interface IAnnouncementGuard
{
  public Task<bool> TellTheDevicesWithoutFailingTheSavedChangeAsync(Func<CancellationToken, Task> tellTheDevices, CancellationToken cancellationToken);
}
