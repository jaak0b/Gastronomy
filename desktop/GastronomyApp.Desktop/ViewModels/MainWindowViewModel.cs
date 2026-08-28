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
  Running,
}

public sealed class MainWindowViewModel : ViewModelBase
{
  private readonly IHostLauncher launcher;
  private readonly IPowerManager power;
  private readonly ISettingsStore settingsStore;
  private readonly IDesktopTextProvider text;
  private readonly IFreePortProvider freePorts;
  private readonly Never never = new();

  private const int MaximumPortAttempts = 10;
  private const string EveryNetworkInterface = "0.0.0.0";

  private HostStatus status = HostStatus.Stopped;
  private int adminPort;
  private LanguageOption? selectedLanguage;
  private readonly AppLanguage appLanguage = new();
  private string? noticeText;
  private string? errorMessageKey;
  private string? errorMessage;

  public MainWindowViewModel(
      IHostLauncher launcher,
      IPowerManager power,
      ISettingsStore settingsStore,
      IDesktopTextProvider text,
      IFreePortProvider freePorts)
  {
    this.launcher = launcher;
    this.power = power;
    this.settingsStore = settingsStore;
    this.text = text;
    this.freePorts = freePorts;

    Languages.Add(new LanguageOption("de", text.Get("desktop.language.german")));
    Languages.Add(new LanguageOption("en", text.Get("desktop.language.english")));

    DesktopSettings startupSettings = settingsStore.Load();
    adminPort = startupSettings.Port ?? 0;

    string storedLanguage = startupSettings.Language
        ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    selectedLanguage = Languages.FirstOrDefault(language => language.Code == storedLanguage)
        ?? Languages.Single(language => language.Code == "en");
    text.UseLanguage(selectedLanguage.Code);
    appLanguage.Current = selectedLanguage.Code;
    text.LanguageChanged += OnLanguageChanged;

    OpenAdminPagesCommand = new RelayCommand(() => AdminPagesRequested?.Invoke(AdminUrl));
    OpenDataFolderCommand = new RelayCommand(() => DataFolderRequested?.Invoke());
    RepairSetupCommand = new RelayCommand(() => RepairRequested?.Invoke());
    RequestQuitCommand = new RelayCommand(() => QuitRequested?.Invoke());
  }

  public event Action<string>? AdminPagesRequested;

  public event Action? DataFolderRequested;

  public event Action? RepairRequested;

  public event Action? QuitRequested;

  public IRelayCommand OpenAdminPagesCommand { get; }

  public IRelayCommand OpenDataFolderCommand { get; }

  public IRelayCommand RepairSetupCommand { get; }

  public IRelayCommand RequestQuitCommand { get; }

  public string WindowTitle => text.Get("desktop.windowTitle");

  public string AdminUrl => $"http://localhost:{adminPort}/admin";

  public string LanguageLabel => text.Get("desktop.language");

  public ObservableCollection<LanguageOption> Languages { get; } = [];

  public LanguageOption? SelectedLanguage
  {
    get => selectedLanguage;
    set
    {
      if (value is null || !SetProperty(ref selectedLanguage, value))
      {
        return;
      }

      text.UseLanguage(value.Code);
      appLanguage.Current = value.Code;
      settingsStore.Save(settingsStore.Load() with { Language = value.Code });
    }
  }

  public string MinimisedText => text.Get("desktop.minimised");

  public string AdminButtonLabel => text.Get("desktop.button.admin");

  public string DataFolderButtonLabel => text.Get("desktop.settings.openDataFolder");

  public string RepairButtonLabel => text.Get("desktop.settings.repairSetup");

  public string QuitButtonLabel => text.Get("desktop.button.quit");

  public HostStatus Status
  {
    get => status;
    private set => SetProperty(ref status, value);
  }

  public string? ErrorMessageKey
  {
    get => errorMessageKey;
    private set
    {
      if (SetProperty(ref errorMessageKey, value))
      {
        OnPropertyChanged(nameof(HasError));
      }
    }
  }

  public string? ErrorMessage
  {
    get => errorMessage;
    private set => SetProperty(ref errorMessage, value);
  }

  public bool HasError => ErrorMessageKey is not null;

  public string? NoticeText
  {
    get => noticeText;
    set
    {
      if (SetProperty(ref noticeText, value))
      {
        OnPropertyChanged(nameof(HasNotice));
      }
    }
  }

  public bool HasNotice => NoticeText is not null;

  private void OnLanguageChanged()
  {
    OnPropertyChanged(string.Empty);
  }

  public async Task StartAsync(CancellationToken cancellationToken = default)
  {
    DesktopSettings settings = settingsStore.Load();
    int? writtenDownPort = settings.Port;
    int port = writtenDownPort ?? freePorts.Reserve();

    for (int attempt = 0; attempt < MaximumPortAttempts; attempt++)
    {
      HostLaunchResult result = await launcher.StartAsync(
          OptionsFor(settings, port),
          cancellationToken);

      if (result is HostLaunchResult.PortInUse)
      {
        Log.Warning("Port {Port} is already in use. Asking Windows for another one.", port);
        port = freePorts.Reserve();

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
    return new ApiHostOptions
    {
      DataDirectory = settings.DataDirectory,
      Port = port,
      BindAddress = EveryNetworkInterface,
      Language = appLanguage,
    };
  }

  private void Apply(HostLaunchResult result, DesktopSettings settings, int? writtenDownPort, int port)
  {
    switch (result)
    {
      case HostLaunchResult.Started:
        Log.Information(
            "The server is answering on port {Port} in {DataDirectory}.",
            port,
            settings.DataDirectory);
        RememberPort(settings, writtenDownPort, port);
        ClearError();
        Status = HostStatus.Running;
        power.PreventSleep();

        break;

      case HostLaunchResult.DataFolderNotWritable notWritable:
        ShowError(
            "desktop.error.dataFolderRepair",
            new TextPlaceholder("path", notWritable.Path));

        break;

      case HostLaunchResult.NoNetworkAvailable:
        ShowError("desktop.error.noNetwork");

        break;

      case HostLaunchResult.StartFailed:
        ShowError("desktop.error.startFailed");

        break;

      default:
        never.OfType<HostLaunchResult>(result);

        break;
    }
  }

  private void RememberPort(DesktopSettings settings, int? writtenDownPort, int port)
  {
    adminPort = port;
    OnPropertyChanged(nameof(AdminUrl));

    if (writtenDownPort == port)
    {
      return;
    }

    settingsStore.Save(settings with { Port = port });

    if (writtenDownPort is not null)
    {
      Log.Warning(
          "The port changed from {PreviousPort} to {Port}. Every phone has to be set up again.",
          writtenDownPort,
          port);
      NoticeText = text.Get("desktop.notice.addressChanged");
    }
  }

  public async Task StopAsync(CancellationToken cancellationToken = default)
  {
    await launcher.StopAsync(cancellationToken);
    Status = HostStatus.Stopped;
    power.AllowSleep();
  }

  public void ShowRepairOutcome(ElevatedSetupOutcome outcome)
  {
    switch (outcome)
    {
      case ElevatedSetupOutcome.Completed:
        NoticeText = text.Get("desktop.settings.repairDone");

        break;

      case ElevatedSetupOutcome.ElevationDeclined:
        NoticeText = text.Get("desktop.settings.repairDeclined");

        break;

      default:
        never.OfType<ElevatedSetupOutcome>(outcome);

        break;
    }
  }

  private void ShowError(string key, params TextPlaceholder[] placeholders)
  {
    ErrorMessageKey = key;
    ErrorMessage = text.Format(key, placeholders);
  }

  private void ClearError()
  {
    ErrorMessageKey = null;
    ErrorMessage = null;
  }
}
