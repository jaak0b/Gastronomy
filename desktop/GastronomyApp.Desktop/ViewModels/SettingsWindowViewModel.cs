using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GastronomyApp.Core.Services;
using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop.ViewModels;

public sealed class SettingsWindowViewModel : ViewModelBase
{
    private const int LowestUsablePort = 1;
    private const int HighestUsablePort = 65535;

    private readonly ISettingsStore settingsStore;
    private readonly INetworkAddressProvider networkAddressProvider;
    private readonly bool serverIsRunning;
    private readonly IElevatedSetupLauncher elevatedSetup;
    private readonly IDesktopTextProvider text;
    private readonly Action openFirewallSettings;
    private readonly Action openDataFolderAction;
    private readonly Never never = new();

    private int port;
    private string bindAddress = string.Empty;
    private string dataDirectory = string.Empty;
    private string? selectedNetworkInterface;
    private bool areAddressFieldsEnabled = true;
    private bool isDataFolderEnabled = true;
    private bool isNetworkSelectorVisible;
    private string? dataFolderRestartText;
    private string? repairDeclinedText;

    public SettingsWindowViewModel(
        ISettingsStore settingsStore,
        INetworkAddressProvider networkAddressProvider,
        bool serverIsRunning,
        IElevatedSetupLauncher elevatedSetup,
        IDesktopTextProvider text,
        Action openFirewallSettings,
        Action openDataFolder)
    {
        this.settingsStore = settingsStore;
        this.networkAddressProvider = networkAddressProvider;
        this.serverIsRunning = serverIsRunning;
        this.elevatedSetup = elevatedSetup;
        this.text = text;
        this.openFirewallSettings = openFirewallSettings;
        openDataFolderAction = openDataFolder;

        this.text.LanguageChanged += OnLanguageChanged;

        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
        RepairSetupCommand = new AsyncRelayCommand(() => RepairSetupAsync());
        SaveCommand = new RelayCommand(Save);
    }

    public IRelayCommand OpenDataFolderCommand { get; }

    public IAsyncRelayCommand RepairSetupCommand { get; }

    public IRelayCommand SaveCommand { get; }

    public string Title => text.Get("desktop.settings.title");

    public string PortLabel => text.Get("desktop.settings.port");

    public string PortHelpText => text.Get("desktop.settings.portHelp");

    public string BindAddressLabel => text.Get("desktop.settings.bindAddress");

    public string BindAddressHelpText => text.Get("desktop.settings.bindAddressHelp");

    public string DataFolderLabel => text.Get("desktop.settings.dataFolder");

    public string DataFolderHelpText => text.Get("desktop.settings.dataFolderHelp");

    public string OpenDataFolderLabel => text.Get("desktop.settings.openDataFolder");

    public string NetworkLabel => text.Get("desktop.settings.network");

    public string NetworkHelpText => text.Get("desktop.settings.networkHelp");

    public string RepairSetupLabel => text.Get("desktop.settings.repairSetup");

    public string RepairSetupHelpText => text.Get("desktop.settings.repairSetupHelp");

    public ObservableCollection<NetworkAddressOption> Networks { get; } = [];

    public int Port
    {
        get => port;
        set
        {
            if (!AreAddressFieldsEnabled)
            {
                return;
            }

            if (SetProperty(ref port, value))
            {
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    public string BindAddress
    {
        get => bindAddress;
        set
        {
            if (!AreAddressFieldsEnabled)
            {
                return;
            }

            if (SetProperty(ref bindAddress, value))
            {
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    public string DataDirectory
    {
        get => dataDirectory;
        set
        {
            if (!IsDataFolderEnabled)
            {
                return;
            }

            if (SetProperty(ref dataDirectory, value))
            {
                DataFolderRestartText = text.Get("desktop.settings.dataFolderRestart");
            }
        }
    }

    public string? SelectedNetworkInterface
    {
        get => selectedNetworkInterface;
        set
        {
            if (SetProperty(ref selectedNetworkInterface, value))
            {
                OnPropertyChanged(nameof(SelectedNetwork));
            }
        }
    }

    public NetworkAddressOption? SelectedNetwork
    {
        get => Networks.FirstOrDefault(option => option.InterfaceName == selectedNetworkInterface);
        set => SelectedNetworkInterface = value?.InterfaceName;
    }

    public bool AreAddressFieldsEnabled
    {
        get => areAddressFieldsEnabled;
        private set
        {
            if (SetProperty(ref areAddressFieldsEnabled, value))
            {
                OnPropertyChanged(nameof(AddressLockedText));
            }
        }
    }

    public string? AddressLockedText => AreAddressFieldsEnabled
        ? null
        : text.Get("desktop.settings.addressLocked");

    public bool IsDataFolderEnabled
    {
        get => isDataFolderEnabled;
        private set
        {
            if (SetProperty(ref isDataFolderEnabled, value))
            {
                OnPropertyChanged(nameof(DataFolderLockedText));
            }
        }
    }

    public string? DataFolderLockedText => IsDataFolderEnabled
        ? null
        : text.Get("desktop.settings.dataFolderLocked");

    public string? DataFolderRestartText
    {
        get => dataFolderRestartText;
        private set => SetProperty(ref dataFolderRestartText, value);
    }

    public string? RepairDeclinedText
    {
        get => repairDeclinedText;
        private set => SetProperty(ref repairDeclinedText, value);
    }

    public bool IsNetworkSelectorVisible
    {
        get => isNetworkSelectorVisible;
        private set => SetProperty(ref isNetworkSelectorVisible, value);
    }

    public bool CanSave => port >= LowestUsablePort
        && port <= HighestUsablePort
        && !string.IsNullOrWhiteSpace(bindAddress)
        && !string.IsNullOrWhiteSpace(dataDirectory);

    private void OnLanguageChanged()
    {
        OnPropertyChanged(string.Empty);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        AreAddressFieldsEnabled = !serverIsRunning;
        IsDataFolderEnabled = !serverIsRunning;

        DesktopSettings settings = settingsStore.Load();
        SetProperty(ref port, settings.Port, nameof(Port));
        SetProperty(ref bindAddress, settings.BindAddress, nameof(BindAddress));
        SetProperty(ref dataDirectory, settings.DataDirectory, nameof(DataDirectory));
        SelectedNetworkInterface = settings.SelectedNetworkInterface;
        DataFolderRestartText = null;
        OnPropertyChanged(nameof(CanSave));

        Networks.Clear();
        foreach (NetworkAddressOption option in networkAddressProvider.GetAvailableAddresses())
        {
            Networks.Add(option);
        }

        IsNetworkSelectorVisible = Networks.Count > 1;
    }

    public void Save()
    {
        if (!CanSave)
        {
            return;
        }

        settingsStore.Save(settingsStore.Load() with
        {
            Port = port,
            BindAddress = bindAddress,
            DataDirectory = dataDirectory,
            SelectedNetworkInterface = SelectedNetworkInterface,
        });
    }

    public void OpenDataFolder()
    {
        openDataFolderAction();
    }

    public async Task RepairSetupAsync(CancellationToken cancellationToken = default)
    {
        ElevatedSetupOutcome outcome = await elevatedSetup.RunElevatedSetupAsync(cancellationToken);

        switch (outcome)
        {
            case ElevatedSetupOutcome.Completed:
                RepairDeclinedText = null;

                break;

            case ElevatedSetupOutcome.ElevationDeclined:
                RepairDeclinedText = text.Get("desktop.settings.repairDeclined");
                openFirewallSettings();

                break;

            default:
                never.OfType<ElevatedSetupOutcome>(outcome);

                break;
        }
    }
}
