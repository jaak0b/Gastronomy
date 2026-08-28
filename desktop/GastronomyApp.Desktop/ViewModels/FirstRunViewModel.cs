using GastronomyApp.Core.Services;
using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop.ViewModels;

public sealed class FirstRunViewModel : ViewModelBase
{
  private readonly IDataFolderSetup dataFolder;
  private readonly IElevatedSetupLauncher elevatedSetup;
  private readonly IFirewallSetup firewall;
  private readonly Never never = new();
  private readonly IDesktopTextProvider text;
  private string? declinedText;

  private bool isSetupOffered;
  private bool readyToStart;

  public FirstRunViewModel(IFirewallSetup firewall,
                           IDataFolderSetup dataFolder,
                           IElevatedSetupLauncher elevatedSetup,
                           IDesktopTextProvider text)
  {
    this.firewall = firewall;
    this.dataFolder = dataFolder;
    this.elevatedSetup = elevatedSetup;
    this.text = text;
  }

  public string Title => text.Get("desktop.firstRun.title");

  public string Body => text.Get("desktop.firstRun.body");

  public string ContinueLabel => text.Get("desktop.firstRun.title");

  public bool IsSetupOffered
  {
    get => isSetupOffered;
    private set => SetProperty(ref isSetupOffered, value);
  }

  public bool ReadyToStart
  {
    get => readyToStart;
    private set => SetProperty(ref readyToStart, value);
  }

  public string? DeclinedText
  {
    get => declinedText;
    private set => SetProperty(ref declinedText, value);
  }

  public void Evaluate()
  {
    var firewallConfigured = firewall.IsRuleConfigured();
    var dataFolderReady = dataFolder.Exists() && dataFolder.CurrentUserCanWrite();
    var everythingInPlace = firewallConfigured && dataFolderReady;

    IsSetupOffered = !everythingInPlace;
    ReadyToStart = everythingInPlace;
    DeclinedText = null;
  }

  public void Decline()
  {
    DeclinedText = text.Get("desktop.firstRun.declined");
    IsSetupOffered = false;
    ReadyToStart = true;
  }

  public async Task RunSetupAsync(CancellationToken cancellationToken = default)
  {
    var outcome = await elevatedSetup.RunElevatedSetupAsync(cancellationToken);

    switch (outcome)
    {
      case ElevatedSetupOutcome.Completed:
        DeclinedText = null;

        break;

      case ElevatedSetupOutcome.ElevationDeclined:
        DeclinedText = text.Get("desktop.firstRun.declined");

        break;

      default:
        never.OfType<ElevatedSetupOutcome>(outcome);

        break;
    }

    IsSetupOffered = false;
    ReadyToStart = true;
  }
}
