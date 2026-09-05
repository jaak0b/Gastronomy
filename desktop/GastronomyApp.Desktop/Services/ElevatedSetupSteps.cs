namespace GastronomyApp.Desktop.Services;

public sealed record ElevatedSetupStepReport(Exception? NetworkAccessFailure, Exception? DataFolderFailure)
{
  public bool EveryStepSucceeded => NetworkAccessFailure is null && DataFolderFailure is null;
}

public sealed class ElevatedSetupSteps
{
  private readonly IDataFolderSetup _dataFolder;
  private readonly IFirewallSetup _firewall;

  public ElevatedSetupSteps(IFirewallSetup firewall, IDataFolderSetup dataFolder)
  {
    _firewall = firewall;
    _dataFolder = dataFolder;
  }

  public ElevatedSetupStepReport RunAll()
  {
    var networkAccessFailure = Attempt(_firewall.EnsureRuleConfigured);
    var dataFolderFailure = Attempt(MakeTheDataFolderWritableForEveryone);

    return new(networkAccessFailure, dataFolderFailure);
  }

  private void MakeTheDataFolderWritableForEveryone()
  {
    if (_dataFolder.Exists())
    {
      _dataFolder.GrantUsersModifyOnExisting();

      return;
    }

    _dataFolder.CreateWithUsersModifyGrant();
  }

  private Exception? Attempt(Action step)
  {
    try
    {
      step();

      return null;
    } catch (Exception failure)
    {
      return failure;
    }
  }
}
