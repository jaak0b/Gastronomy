using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GastronomyApp.Api.Options;
using GastronomyApp.Core.Services;
using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop.ViewModels;

public enum HostStatus
{
    Stopped,
    Running,
}

public enum AttentionState
{
    None,
    Some,
}

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IHostLauncher launcher;
    private readonly IPowerManager power;
    private readonly IQrCodeGenerator qrCodeGenerator;
    private readonly INetworkAddressProvider networkAddressProvider;
    private readonly ISettingsStore settingsStore;
    private readonly IDesktopTextProvider text;
    private readonly Never never = new();

    private HostStatus status = HostStatus.Stopped;
    private AttentionState attention = AttentionState.None;
    private int phoneCount;
    private string addressUrl = string.Empty;
    private string qrContent = string.Empty;
    private string? noticeText;
    private string? errorMessageKey;
    private string? errorMessage;

    public MainWindowViewModel(
        IHostLauncher launcher,
        IPowerManager power,
        IQrCodeGenerator qrCodeGenerator,
        INetworkAddressProvider networkAddressProvider,
        ISettingsStore settingsStore,
        IDesktopTextProvider text)
    {
        this.launcher = launcher;
        this.power = power;
        this.qrCodeGenerator = qrCodeGenerator;
        this.networkAddressProvider = networkAddressProvider;
        this.settingsStore = settingsStore;
        this.text = text;

        OpenAdminPagesCommand = new RelayCommand(() => AdminPagesRequested?.Invoke());
        OpenSettingsCommand = new RelayCommand(() => SettingsRequested?.Invoke());
        RequestQuitCommand = new RelayCommand(() => QuitRequested?.Invoke());
    }

    public event Action? AdminPagesRequested;

    public event Action? SettingsRequested;

    public event Action? QuitRequested;

    public IRelayCommand OpenAdminPagesCommand { get; }

    public IRelayCommand OpenSettingsCommand { get; }

    public IRelayCommand RequestQuitCommand { get; }

    public string WindowTitle => text.Get("desktop.windowTitle");

    public string AddressLabel => text.Get("desktop.addressLabel");

    public string QrHelpText => text.Get("desktop.qrHelp");

    public string MinimisedText => text.Get("desktop.minimised");

    public string AdminButtonLabel => text.Get("desktop.button.admin");

    public string SettingsButtonLabel => text.Get("desktop.button.settings");

    public string QuitButtonLabel => text.Get("desktop.button.quit");

    public ObservableCollection<bool[]> QrMatrix { get; } = [];

    public HostStatus Status
    {
        get => status;
        private set
        {
            if (SetProperty(ref status, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string StatusText => text.Get(Status switch
    {
        HostStatus.Running => "desktop.status.running",
        HostStatus.Stopped => "desktop.status.stopped",
        _ => never.OfType<string>(Status),
    });

    public bool HasAttention
    {
        get => attention == AttentionState.Some;
        set
        {
            AttentionState requested = value ? AttentionState.Some : AttentionState.None;
            if (SetProperty(ref attention, requested, nameof(HasAttention)))
            {
                OnPropertyChanged(nameof(Attention));
                OnPropertyChanged(nameof(AttentionTextKey));
                OnPropertyChanged(nameof(AttentionText));
            }
        }
    }

    public AttentionState Attention => attention;

    public string AttentionTextKey => Attention switch
    {
        AttentionState.None => "desktop.attention.none",
        AttentionState.Some => "desktop.attention.some",
        _ => never.OfType<string>(Attention),
    };

    public string AttentionText => text.Get(AttentionTextKey);

    public int PhoneCount
    {
        get => phoneCount;
        set
        {
            if (SetProperty(ref phoneCount, value))
            {
                OnPropertyChanged(nameof(PhonesTextKey));
                OnPropertyChanged(nameof(PhonesText));
            }
        }
    }

    public string PhonesTextKey => PhoneCount switch
    {
        0 => "desktop.phones.none",
        1 => "desktop.phones.one",
        _ => "desktop.phones.many",
    };

    public string PhonesText => PhoneCount > 1
        ? text.Format("desktop.phones.many", new TextPlaceholder("count", PhoneCount.ToString()))
        : text.Get(PhonesTextKey);

    public string AddressUrl
    {
        get => addressUrl;
        private set
        {
            if (SetProperty(ref addressUrl, value))
            {
                OnPropertyChanged(nameof(AddressText));
            }
        }
    }

    public string AddressText => text.Format("desktop.address", new TextPlaceholder("url", AddressUrl));

    public string QrContent
    {
        get => qrContent;
        private set => SetProperty(ref qrContent, value);
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

    public void RefreshAddress()
    {
        DesktopSettings settings = settingsStore.Load();
        IReadOnlyList<NetworkAddressOption> options = networkAddressProvider.GetAvailableAddresses();

        NetworkAddressOption? chosen = options.FirstOrDefault(
            option => option.InterfaceName == settings.SelectedNetworkInterface) ?? options.FirstOrDefault();

        if (chosen is null)
        {
            AddressUrl = string.Empty;
            QrContent = string.Empty;
            QrMatrix.Clear();

            return;
        }

        AddressUrl = $"http://{chosen.IPAddress}:{settings.Port}";
        QrContent = AddressUrl;

        QrMatrix.Clear();
        foreach (bool[] row in qrCodeGenerator.GenerateMatrix(QrContent))
        {
            QrMatrix.Add(row);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        RefreshAddress();

        DesktopSettings settings = settingsStore.Load();
        ApiHostOptions options = new()
        {
            DataDirectory = settings.DataDirectory,
            Port = settings.Port,
            BindAddress = settings.BindAddress,
        };

        HostLaunchResult result = await launcher.StartAsync(options, cancellationToken);

        switch (result)
        {
            case HostLaunchResult.Started:
                ClearError();
                Status = HostStatus.Running;
                power.PreventSleep();

                break;

            case HostLaunchResult.PortInUse portInUse:
                ShowError(
                    "desktop.error.portInUse",
                    new TextPlaceholder("port", portInUse.Port.ToString()));

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

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await launcher.StopAsync(cancellationToken);
        Status = HostStatus.Stopped;
        power.AllowSleep();
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
