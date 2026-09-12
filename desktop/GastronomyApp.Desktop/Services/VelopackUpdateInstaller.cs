using Serilog;
using Velopack;
using Velopack.Sources;

namespace GastronomyApp.Desktop.Services;

public sealed class VelopackUpdateInstaller : IUpdateInstaller
{
  private const string UpdateFeedUrl = "https://github.com/jaak0b/Gastronomy";

  private readonly UpdateManager _manager;
  private readonly SemaphoreSlim _oneTransferAtATime = new(1, 1);
  private bool _installScheduled;
  private VelopackAsset? _downloadedRelease;

  public VelopackUpdateInstaller()
  {
    _manager = new(new GithubSource(UpdateFeedUrl, null, false));
  }

  public bool IsInstalled => _manager.IsInstalled;

  public bool HasDownloadedUpdate => _downloadedRelease is not null
                                     || (IsInstalled && _manager.UpdatePendingRestart is not null);

  public async Task<UpdatePreparation> CheckAndDownloadAsync(CancellationToken cancellationToken)
  {
    if (!IsInstalled)
    {
      return new UpdatePreparation.UpToDate();
    }

    await _oneTransferAtATime.WaitAsync(cancellationToken);

    try
    {
      var update = await _manager.CheckForUpdatesAsync();

      if (update is null)
      {
        return new UpdatePreparation.UpToDate();
      }

      await _manager.DownloadUpdatesAsync(update, cancelToken: cancellationToken);
      _downloadedRelease = update.TargetFullRelease;

      return new UpdatePreparation.Ready(update.TargetFullRelease.Version.ToString());
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception failure)
    {
      Log.Error(failure, "Checking for or downloading an update failed.");
      return new UpdatePreparation.Failed(failure);
    }
    finally
    {
      _oneTransferAtATime.Release();
    }
  }

  public void InstallOnQuit(bool restart)
  {
    if (_installScheduled)
    {
      return;
    }

    var release = _downloadedRelease ?? (IsInstalled ? _manager.UpdatePendingRestart : null);

    if (release is null)
    {
      return;
    }

    _manager.WaitExitThenApplyUpdates(release, silent: true, restart: restart);
    _installScheduled = true;
    Log.Information("A downloaded update is applied when the program exits. Restart requested: {Restart}.",
                    restart);
  }
}
