using Serilog;

namespace GastronomyApp.Desktop.Services;

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
    var networkAccessSucceeded = Attempt(_firewall.EnsureRuleConfigured, "The one-time setup could not allow incoming connections through " + "the Windows firewall, so the phones may not be able to reach " + "this laptop.");

    var dataFolderSucceeded = Attempt(MakeTheDataFolderWritableForEveryone, "The one-time setup could not give every user of this laptop write " + "access to the data folder, so orders may fail for anybody who did " + "not set this laptop up.");

    return new(networkAccessSucceeded, dataFolderSucceeded);
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

  private bool Attempt(Action step, string failureLogMessage)
  {
    try
    {
      step();

      return true;
    }
    catch (Exception failure)
    {
      Log.Error(failure, failureLogMessage);

      return false;
    }
  }
}
