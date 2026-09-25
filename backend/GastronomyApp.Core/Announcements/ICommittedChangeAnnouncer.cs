namespace GastronomyApp.Core.Announcements;

public interface ICommittedChangeAnnouncer
{
  public Task AnnounceAsync(HubEvent hubEvent, CancellationToken cancellationToken);
}
