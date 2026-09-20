using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Endpoints;

public sealed class SavedChangeAnnouncement
{
  private readonly IHostApplicationLifetime _applicationLifetime;
  private readonly ILogger<SavedChangeAnnouncement> _logger;

  public SavedChangeAnnouncement(IHostApplicationLifetime applicationLifetime, ILogger<SavedChangeAnnouncement> logger)
  {
    _applicationLifetime = applicationLifetime;
    _logger = logger;
  }

  public async Task<bool> TellTheDevicesWithoutFailingTheSavedChangeAsync(Func<CancellationToken, Task> tellTheDevices)
  {
    ArgumentNullException.ThrowIfNull(tellTheDevices);

    var tokenOutlivingTheAdminsRequest = _applicationLifetime.ApplicationStopping;

    try
    {
      await tellTheDevices(tokenOutlivingTheAdminsRequest);

      return true;
    }
    catch (OperationCanceledException) when (tokenOutlivingTheAdminsRequest.IsCancellationRequested)
    {
      _logger.LogInformation("The change was saved, but the program was quitting, so the phones and station tablets were not told about it and will load it the next time they connect.");

      return false;
    }
    catch (Exception exception)
    {
      _logger.LogError(exception, "The change was saved, but the phones and station tablets could not be told about it, so they keep showing what they loaded before until they load it again.");

      return false;
    }
  }
}
