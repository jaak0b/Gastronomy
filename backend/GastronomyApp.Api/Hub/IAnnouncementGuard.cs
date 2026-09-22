namespace GastronomyApp.Api.Hub;

public interface IAnnouncementGuard
{
  public Task TellTheDevicesWithoutFailingTheSavedChangeAsync(Func<CancellationToken, Task> tellTheDevices, CancellationToken cancellationToken);
}
