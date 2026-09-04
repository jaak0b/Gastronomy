using GastronomyApp.Core.Services;
using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop.ViewModels;

public sealed class FirstRunViewModel : ViewModelBase
{
  private readonly IDataFolderSetup _dataFolder;
  private readonly IElevatedSetupLauncher _elevatedSetup;
  private readonly IFirewallSetup _firewall;
  private readonly Never _never = new();
  private readonly IDesktopTextProvider _text;
  private string? _declinedText;

  private bool _isSetupOffered;
  private bool _readyToStart;

  public FirstRunViewModel(IFirewallSetup firewall,
                           IDataFolderSetup dataFolder,
                           IElevatedSetupLauncher elevatedSetup,
                           IDesktopTextProvider text)
  {
    _firewall = firewall;
    _dataFolder = dataFolder;
    _elevatedSetup = elevatedSetup;
    _text = text;
  }

  public string Title => _text.Get("desktop.firstRun.title");

  public string Body => _text.Get("desktop.firstRun.body");

  public string ContinueLabel => _text.Get("desktop.firstRun.title");

  public bool IsSetupOffered
  {
    get => _isSetupOffered;
    private set => SetProperty(ref _isSetupOffered, value);
  }

  public bool ReadyToStart
  {
    get => _readyToStart;
    private set => SetProperty(ref _readyToStart, value);
  }

  public string? DeclinedText
  {
    get => _declinedText;
    private set => SetProperty(ref _declinedText, value);
  }

  public void Evaluate()
  {
    var firewallConfigured = _firewall.IsRuleConfigured();
    var dataFolderReady = _dataFolder.Exists() && _dataFolder.CurrentUserCanWrite();
    var everythingInPlace = firewallConfigured && dataFolderReady;

    IsSetupOffered = !everythingInPlace;
    ReadyToStart = everythingInPlace;
    DeclinedText = null;
  }

  public void Decline()
  {
    DeclinedText = _text.Get("desktop.firstRun.declined");
    IsSetupOffered = false;
    ReadyToStart = true;
  }

  public async Task RunSetupAsync(CancellationToken cancellationToken = default)
  {
    var outcome = await _elevatedSetup.RunElevatedSetupAsync(cancellationToken);

    switch (outcome)
    {
      case ElevatedSetupOutcome.Completed:
        DeclinedText = null;

        break;

      case ElevatedSetupOutcome.ElevationDeclined:
        DeclinedText = _text.Get("desktop.firstRun.declined");

        break;

      default:
        _never.OfType<ElevatedSetupOutcome>(outcome);

        break;
    }

    IsSetupOffered = false;
    ReadyToStart = true;
  }
}
