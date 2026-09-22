using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Hub;

public sealed class AnnouncementGuard : IAnnouncementGuard
{
  private readonly IHostApplicationLifetime _applicationLifetime;
  private readonly ILogger<AnnouncementGuard> _logger;

  public AnnouncementGuard(IHostApplicationLifetime applicationLifetime, ILogger<AnnouncementGuard> logger)
  {
    _applicationLifetime = applicationLifetime;
    _logger = logger;
  }

  public async Task TellTheDevicesWithoutFailingTheSavedChangeAsync(Func<CancellationToken, Task> tellTheDevices, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(tellTheDevices);

    try
    {
      await tellTheDevices(cancellationToken);
    }
    catch (OperationCanceledException) when (_applicationLifetime.ApplicationStopping.IsCancellationRequested)
    {
      _logger.LogInformation("The change was saved, but the program was quitting, so the phones and station tablets were not told about it and will load it the next time they connect.");
    }
    catch (Exception exception)
    {
      _logger.LogError(exception, "The change was saved, but the phones and station tablets could not be told about it, so they keep showing what they loaded before until they load it again.");
    }
  }
}
