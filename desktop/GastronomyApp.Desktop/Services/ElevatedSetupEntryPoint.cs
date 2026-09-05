using Serilog;

namespace GastronomyApp.Desktop.Services;

public sealed class ElevatedSetupEntryPoint
{
  private const int EverythingSucceeded = 0;
  private const int AStepFailed = 2;

  private readonly string _dataDirectoryPath;
  private readonly ApplicationLog _log;
  private readonly ElevatedSetupSteps _steps;

  public ElevatedSetupEntryPoint(ApplicationLog log, string dataDirectoryPath, ElevatedSetupSteps steps)
  {
    _log = log;
    _dataDirectoryPath = dataDirectoryPath;
    _steps = steps;
  }

  public int Run()
  {
    try
    {
      _log.Start(_dataDirectoryPath);

      return ExitCodeFor(_steps.RunAll());
    } catch (Exception failure)
    {
      Log.Error(failure, "The one-time setup stopped before it could report what it managed to do.");

      return AStepFailed;
    }
  }

  private int ExitCodeFor(ElevatedSetupStepReport report)
  {
    if (report.NetworkAccessFailure is not null)
    {
      Log.Error(report.NetworkAccessFailure,
                "The one-time setup could not allow incoming connections through the Windows firewall, "
                + "so the phones may not be able to reach this laptop.");
    }

    if (report.DataFolderFailure is not null)
    {
      Log.Error(report.DataFolderFailure,
                "The one-time setup could not give every user of this laptop write access to {DataDirectory}, "
                + "so orders may fail for anybody who did not set this laptop up.",
                _dataDirectoryPath);
    }

    if (!report.EveryStepSucceeded)
    {
      return AStepFailed;
    }

    Log.Information("The one-time setup finished. Incoming connections are allowed through the Windows "
                    + "firewall and every user of this laptop can write into {DataDirectory}.",
                    _dataDirectoryPath);

    return EverythingSucceeded;
  }
}
