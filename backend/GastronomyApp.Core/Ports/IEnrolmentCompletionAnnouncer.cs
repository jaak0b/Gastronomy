using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IEnrolmentCompletionAnnouncer
{
  public Task AnnounceEnrolmentCompletedAsync(IDeviceOwner owner, CancellationToken cancellationToken);
}
