using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using GastronomyApp.Api.Options;
using GastronomyApp.Core.Services;
using GastronomyApp.Desktop.Services;
using Serilog;

namespace GastronomyApp.Desktop.ViewModels;

public enum HostStatus
{
  Stopped,
  Running
}

public enum StatusLevel
{
  Starting,
  Running,
  Warning,
  Down
}

public enum NoticeLevel
{
  Informational,
  Warning
}

public sealed class MainWindowViewModel : ViewModelBase
{

  private const int MaximumPortAttempts = 10;
  private const string EveryNetworkInterface = "0.0.0.0";
  private readonly AppLanguage _appLanguage = new();
  private readonly string _currentVersion;
  private readonly IFreePortProvider _freePorts;
  private readonly IHostLauncher _launcher;
  private readonly Never _never = new();
  private readonly IPowerManager _power;
  private readonly ISettingsStore _settingsStore;
  private readonly IDesktopTextProvider _text;
  private readonly IUpdateInstaller _updateInstaller;
  private int _adminPort;
  private string? _errorMessageKey;
  private TextPlaceholder[] _errorPlaceholders = [];
  private string? _failureDetail;
  private bool _isUpdateCheckRunning;
  private StatusNotice? _notice;
  private LanguageOption? _selectedLanguage;

  private HostStatus _status = HostStatus.Stopped;

  public MainWindowViewModel(IHostLauncher launcher,
                             IPowerManager power,
                             ISettingsStore settingsStore,
                             IDesktopTextProvider text,
                             IFreePortProvider freePorts,
                             IUpdateInstaller updateInstaller,
                             string currentVersion)
  {
    _launcher = launcher;
    _power = power;
    _settingsStore = settingsStore;
    _text = text;
    _freePorts = freePorts;
    _updateInstaller = updateInstaller;
    _currentVersion = currentVersion;

    Languages.Add(new("de", text.Get("desktop.language.german")));
    Languages.Add(new("en", text.Get("desktop.language.english")));

    var startupSettings = settingsStore.Load();
    _adminPort = startupSettings.Port ?? 0;

    var storedLanguage = startupSettings.Language
                         ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    _selectedLanguage = Languages.FirstOrDefault(language => language.Code == storedLanguage)
                       ?? Languages.Single(language => language.Code == "en");
    text.UseLanguage(_selectedLanguage.Code);
    _appLanguage.Current = _selectedLanguage.Code;
    text.LanguageChanged += OnLanguageChanged;

    OpenAdminPagesCommand = new RelayCommand(() => AdminPagesRequested?.Invoke(AdminUrl));
    OpenDataFolderCommand = new RelayCommand(() => DataFolderRequested?.Invoke());
    RepairSetupCommand = new RelayCommand(() => RepairRequested?.Invoke());
    RequestQuitCommand = new RelayCommand(() => QuitRequested?.Invoke());
    ShowFailureDetailCommand = new RelayCommand(ShowFailureDetail);
    CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync, () => !IsUpdateCheckRunning);
  }

  public IRelayCommand OpenAdminPagesCommand { get; }

  public IRelayCommand OpenDataFolderCommand { get; }

  public IRelayCommand RepairSetupCommand { get; }

  public IRelayCommand RequestQuitCommand { get; }

  public IRelayCommand ShowFailureDetailCommand { get; }

  public IAsyncRelayCommand CheckForUpdatesCommand { get; }

  public string WindowTitle => _text.Get("desktop.windowTitle");

  public string VersionText => _text.Format("desktop.version", new TextPlaceholder("version", _currentVersion));

  public bool CanCheckForUpdates => _updateInstaller.IsInstalled;

  public string AdminUrl => $"http://localhost:{_adminPort}/admin";

  public string LanguageLabel => _text.Get("desktop.language");

  public ObservableCollection<LanguageOption> Languages { get; } = [];

  public LanguageOption? SelectedLanguage
  {
    get => _selectedLanguage;
    set
    {
      if (value is null || !SetProperty(ref _selectedLanguage, value))
      {
        return;
      }

      _text.UseLanguage(value.Code);
      _appLanguage.Current = value.Code;
      _settingsStore.Save(_settingsStore.Load() with { Language = value.Code });
    }
  }

  public string MinimisedText => _text.Get("desktop.minimised");

  public string AdminButtonLabel => _text.Get("desktop.button.admin");

  public string DataFolderButtonLabel => _text.Get("desktop.settings.openDataFolder");

  public string RepairButtonLabel => _text.Get("desktop.settings.repairSetup");

  public string QuitButtonLabel => _text.Get("desktop.button.quit");

  public HostStatus Status
  {
    get => _status;
    private set
    {
      if (SetProperty(ref _status, value))
      {
        RaiseStatusChanged();
      }
    }
  }

  public bool IsUpdateCheckRunning
  {
    get => _isUpdateCheckRunning;
    private set
    {
      if (SetProperty(ref _isUpdateCheckRunning, value))
      {
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
      }
    }
  }

  public string? ErrorMessageKey => _errorMessageKey;

  public string? ErrorMessage => _errorMessageKey is null
                                  ? null
                                  : _text.Format(_errorMessageKey, _errorPlaceholders);

  public bool HasError => _errorMessageKey is not null;

  public string? FailureDetail => _failureDetail;

  public bool CanShowFailureDetail => _failureDetail is not null;

  public string FailureDetailButtonLabel => _text.Get("desktop.error.showFailureDetail");

  public string FailureDetailTitle => _text.Get("desktop.error.failureDetailTitle");

  public string FailureDetailCloseLabel => _text.Get("desktop.error.failureDetailClose");

  public string? NoticeText => _notice is null ? null : _text.Get(_notice.Key);

  public bool HasNotice => _notice is not null;

  public StatusLevel CurrentStatus
  {
    get
    {
      if (HasError)
      {
        return StatusLevel.Down;
      }

      if (_notice?.Level is NoticeLevel.Warning)
      {
        return StatusLevel.Warning;
      }

      return Status is HostStatus.Running ? StatusLevel.Running : StatusLevel.Starting;
    }
  }

  public string? StatusText => CurrentStatus is StatusLevel.Down ? ErrorMessage : NoticeText;

  public bool HasStatusText => StatusText is not null;

  public event Action<string>? AdminPagesRequested;

  public event Action? DataFolderRequested;

  public event Action? RepairRequested;

  public event Action? QuitRequested;

  public event Action? FailureDetailRequested;

  public event Action<string>? UpdateReadyRequested;

  public event Action<Exception>? UpdateFailureRequested;

  private void OnLanguageChanged()
  {
    OnPropertyChanged(string.Empty);
  }

  public async Task StartAsync(CancellationToken cancellationToken = default)
  {
    var settings = _settingsStore.Load();
    var writtenDownPort = settings.Port;
    var port = writtenDownPort ?? _freePorts.Reserve();

    for (var attempt = 0; attempt < MaximumPortAttempts; attempt++)
    {
      var result = await _launcher.StartAsync(OptionsFor(settings, port),
                                             cancellationToken);

      if (result is HostLaunchResult.PortInUse)
      {
        Log.Warning("Port {Port} is already in use. Asking Windows for another one.", port);
        port = _freePorts.Reserve();

        continue;
      }

      if (result is HostLaunchResult.StartFailed failed)
      {
        Log.Error(failed.Failure, "The server could not be started on port {Port}.", port);
      }

      Apply(result, settings, writtenDownPort, port);

      return;
    }

    Log.Error("No free port could be found after {Attempts} attempts.", MaximumPortAttempts);
    ShowError("desktop.error.noPortAvailable");
  }

  private ApiHostOptions OptionsFor(DesktopSettings settings, int port)
  {
    return new()
           {
             DataDirectory = settings.DataDirectory,
             Port = port,
             BindAddress = EveryNetworkInterface,
             Language = _appLanguage
           };
  }

  private void Apply(HostLaunchResult result, DesktopSettings settings, int? writtenDownPort, int port)
  {
    switch (result)
    {
      case HostLaunchResult.Started:
        Log.Information("The server is answering on port {Port} in {DataDirectory}.",
                        port,
                        settings.DataDirectory);
        RememberPort(settings, writtenDownPort, port);
        ClearError();
        Status = HostStatus.Running;
        _power.PreventSleep();

        break;

      case HostLaunchResult.DataFolderNotWritable notWritable:
        ShowError("desktop.error.dataFolderRepair",
                  new TextPlaceholder("path", notWritable.Path));

        break;

      case HostLaunchResult.NoNetworkAvailable:
        ShowError("desktop.error.noNetwork");

        break;

      case HostLaunchResult.StartFailed failedToStart:
        ShowError("desktop.error.startFailed");
        KeepFailureDetail(failedToStart.Failure);

        break;

      default:
        _never.OfType<HostLaunchResult>(result);

        break;
    }
  }

  private void RememberPort(DesktopSettings settings, int? writtenDownPort, int port)
  {
    _adminPort = port;
    OnPropertyChanged(nameof(AdminUrl));

    if (writtenDownPort == port)
    {
      return;
    }

    _settingsStore.Save(settings with { Port = port });

    if (writtenDownPort is not null)
    {
      Log.Warning("The port changed from {PreviousPort} to {Port}. Every phone has to be set up again.",
                  writtenDownPort,
                  port);
      ShowNotice("desktop.notice.addressChanged", NoticeLevel.Warning);
    }
  }

  public async Task StopAsync(CancellationToken cancellationToken = default)
  {
    await _launcher.StopAsync(cancellationToken);
    Status = HostStatus.Stopped;
    _power.AllowSleep();
  }

  private async Task CheckForUpdatesAsync()
  {
    if (IsUpdateCheckRunning)
    {
      return;
    }

    IsUpdateCheckRunning = true;

    try
    {
      var preparation = await _updateInstaller.CheckAndDownloadAsync(CancellationToken.None);

      switch (preparation)
      {
        case UpdatePreparation.Ready ready:
          UpdateReadyRequested?.Invoke(ready.Version);

          break;

        case UpdatePreparation.Failed failed:
          UpdateFailureRequested?.Invoke(failed.Failure);

          break;

        case UpdatePreparation.UpToDate:
          break;

        default:
          _never.OfType<UpdatePreparation>(preparation);

          break;
      }
    } finally
    {
      IsUpdateCheckRunning = false;
    }
  }

  public void ShowRepairOutcome(ElevatedSetupOutcome outcome)
  {
    switch (outcome)
    {
      case ElevatedSetupOutcome.Completed:
        ShowNotice("desktop.settings.repairDone", NoticeLevel.Informational);

        break;

      case ElevatedSetupOutcome.ElevationDeclined:
        ShowNotice("desktop.settings.repairDeclined", NoticeLevel.Warning);

        break;

      case ElevatedSetupOutcome.SetupStepFailed:
        ShowNotice("desktop.settings.repairFailed", NoticeLevel.Warning);

        break;

      default:
        _never.OfType<ElevatedSetupOutcome>(outcome);

        break;
    }
  }

  public void ShowInstanceCheckFailed()
  {
    ShowError("desktop.error.instanceCheckFailed");
  }

  public void ShowSetupDeclined()
  {
    ShowNotice("desktop.firstRun.declined", NoticeLevel.Warning);
  }

  public void ShowSetupFailed()
  {
    ShowNotice("desktop.firstRun.setupFailed", NoticeLevel.Warning);
  }

  private void ShowNotice(string key, NoticeLevel level)
  {
    _notice = new(key, level);
    OnPropertyChanged(nameof(NoticeText));
    OnPropertyChanged(nameof(HasNotice));
    RaiseStatusChanged();
  }

  private void ShowError(string key, params TextPlaceholder[] placeholders)
  {
    _errorMessageKey = key;
    _errorPlaceholders = placeholders;
    RaiseErrorChanged();
  }

  private void ClearError()
  {
    _errorMessageKey = null;
    _errorPlaceholders = [];
    ForgetFailureDetail();
    RaiseErrorChanged();
  }

  private void KeepFailureDetail(Exception failure)
  {
    _failureDetail = failure.ToString();
    RaiseFailureDetailChanged();
  }

  private void ForgetFailureDetail()
  {
    _failureDetail = null;
    RaiseFailureDetailChanged();
  }

  private void ShowFailureDetail()
  {
    if (_failureDetail is null)
    {
      return;
    }

    FailureDetailRequested?.Invoke();
  }

  private void RaiseFailureDetailChanged()
  {
    OnPropertyChanged(nameof(FailureDetail));
    OnPropertyChanged(nameof(CanShowFailureDetail));
  }

  private void RaiseErrorChanged()
  {
    OnPropertyChanged(nameof(ErrorMessageKey));
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(HasError));
    RaiseStatusChanged();
  }

  private void RaiseStatusChanged()
  {
    OnPropertyChanged(nameof(CurrentStatus));
    OnPropertyChanged(nameof(StatusText));
    OnPropertyChanged(nameof(HasStatusText));
  }

  private sealed record StatusNotice(string Key, NoticeLevel Level);
}
