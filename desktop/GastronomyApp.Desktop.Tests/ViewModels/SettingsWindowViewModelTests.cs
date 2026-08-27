using FakeItEasy;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Services;
using GastronomyApp.Desktop.ViewModels;

namespace GastronomyApp.Desktop.Tests.ViewModels;

[TestFixture]
public sealed class SettingsWindowViewModelTests
{
    private const string DataFolder = @"C:\ProgramData\GastronomyApp";

    private ISettingsStore _settingsStore = null!;
    private INetworkAddressProvider _networkAddressProvider = null!;
    private ISessionStateQuery _sessionState = null!;
    private IElevatedSetupLauncher _elevatedSetup = null!;
    private IDesktopTextProvider _text = null!;
    private int _firewallSettingsOpened;
    private int _dataFolderOpened;

    [SetUp]
    public void SetUp()
    {
        _settingsStore = A.Fake<ISettingsStore>();
        _networkAddressProvider = A.Fake<INetworkAddressProvider>();
        _sessionState = A.Fake<ISessionStateQuery>();
        _elevatedSetup = A.Fake<IElevatedSetupLauncher>();
        _text = new DesktopTextProvider();
        _firewallSettingsOpened = 0;
        _dataFolderOpened = 0;

        A.CallTo(() => _settingsStore.Load())
            .Returns(new DesktopSettings(5000, "0.0.0.0", DataFolder, null, null));
        A.CallTo(() => _networkAddressProvider.GetAvailableAddresses())
            .Returns(new List<NetworkAddressOption> { new("WiFi", "192.168.1.20") });
        A.CallTo(() => _sessionState.IsSessionActiveAsync(A<CancellationToken>._)).Returns(false);
    }

    private async Task<SettingsWindowViewModel> CreateOpenedViewModelAsync(bool anyOrderAcceptedThisSession = false)
    {
        SettingsWindowViewModel viewModel = new(
            _settingsStore,
            _networkAddressProvider,
            _sessionState,
            _elevatedSetup,
            _text,
            () => _firewallSettingsOpened++,
            () => _dataFolderOpened++,
            anyOrderAcceptedThisSession);

        await viewModel.InitializeAsync();

        return viewModel;
    }

    [Test]
    public async Task InitializeAsync_BeforeAnyOrderAndWithNoSession_LeavesPortAndAddressEditable()
    {
        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.AreAddressFieldsEnabled, Is.True);
            Assert.That(viewModel.AddressLockedText, Is.Null);
            Assert.That(viewModel.Port, Is.EqualTo(5000));
            Assert.That(viewModel.BindAddress, Is.EqualTo("0.0.0.0"));
        });
    }

    [Test]
    public async Task InitializeAsync_OnceTheSessionHasAcceptedAnOrder_RefusesPortAndAddress()
    {
        A.CallTo(() => _sessionState.IsSessionActiveAsync(A<CancellationToken>._)).Returns(true);

        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync(anyOrderAcceptedThisSession: true);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.AreAddressFieldsEnabled, Is.False);
            Assert.That(viewModel.AddressLockedText, Is.EqualTo(_text.Get("desktop.settings.addressLocked")));
        });
    }

    [Test]
    public async Task Port_WhenTheAddressFieldsAreRefused_KeepsTheStoredValue()
    {
        A.CallTo(() => _sessionState.IsSessionActiveAsync(A<CancellationToken>._)).Returns(true);
        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync(anyOrderAcceptedThisSession: true);

        viewModel.Port = 8080;
        viewModel.BindAddress = "127.0.0.1";

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Port, Is.EqualTo(5000));
            Assert.That(viewModel.BindAddress, Is.EqualTo("0.0.0.0"));
        });
    }

    [Test]
    public async Task InitializeAsync_WhileASessionIsActive_DisablesTheDataFolderField()
    {
        A.CallTo(() => _sessionState.IsSessionActiveAsync(A<CancellationToken>._)).Returns(true);

        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsDataFolderEnabled, Is.False);
            Assert.That(viewModel.DataFolderLockedText, Is.EqualTo(_text.Get("desktop.settings.dataFolderLocked")));
        });
    }

    [Test]
    public async Task DataDirectory_WhileASessionIsActive_IsNeverChanged()
    {
        A.CallTo(() => _sessionState.IsSessionActiveAsync(A<CancellationToken>._)).Returns(true);
        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync();

        viewModel.DataDirectory = @"D:\Somewhere else";

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.DataDirectory, Is.EqualTo(DataFolder));
            Assert.That(viewModel.DataFolderRestartText, Is.Null);
        });
    }

    [Test]
    public async Task InitializeAsync_WithNoSessionRunning_EnablesTheDataFolderField()
    {
        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsDataFolderEnabled, Is.True);
            Assert.That(viewModel.DataFolderLockedText, Is.Null);
        });
    }

    [Test]
    public async Task InitializeAsync_WhenTheServerNeverStarted_LeavesTheDataFolderEditableOnPurpose()
    {
        A.CallTo(() => _sessionState.IsSessionActiveAsync(A<CancellationToken>._)).Returns(false);

        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsDataFolderEnabled, Is.True);
            Assert.That(viewModel.DataFolderLockedText, Is.Null);
        });
    }

    [Test]
    public async Task DataDirectory_WhenChanged_MovesNothingAndAsksForARestart()
    {
        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync();

        viewModel.DataDirectory = @"D:\Festival";

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.DataDirectory, Is.EqualTo(@"D:\Festival"));
            Assert.That(viewModel.DataFolderRestartText, Is.EqualTo(_text.Get("desktop.settings.dataFolderRestart")));
        });
        A.CallTo(() => _settingsStore.Save(A<DesktopSettings>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task RepairSetupAsync_WhenTheElevationIsAccepted_SaysNothingAndOpensNoWindowsSettings()
    {
        A.CallTo(() => _elevatedSetup.RunElevatedSetupAsync(A<CancellationToken>._))
            .Returns(ElevatedSetupOutcome.Completed);
        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync();

        await viewModel.RepairSetupAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.RepairDeclinedText, Is.Null);
            Assert.That(_firewallSettingsOpened, Is.Zero);
        });
        A.CallTo(() => _elevatedSetup.RunElevatedSetupAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RepairSetupAsync_WhenTheElevationIsDeclined_ShowsTheInstructionsAndOpensWindowsFirewallSettings()
    {
        A.CallTo(() => _elevatedSetup.RunElevatedSetupAsync(A<CancellationToken>._))
            .Returns(ElevatedSetupOutcome.ElevationDeclined);
        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync();

        await viewModel.RepairSetupAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.RepairDeclinedText, Is.EqualTo(_text.Get("desktop.settings.repairDeclined")));
            Assert.That(_firewallSettingsOpened, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task OpenDataFolder_InvokesTheDelegatedFolderAction()
    {
        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync();

        viewModel.OpenDataFolder();

        Assert.That(_dataFolderOpened, Is.EqualTo(1));
    }

    [Test]
    public async Task InitializeAsync_WithOneNetwork_HidesTheNetworkSelector()
    {
        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsNetworkSelectorVisible, Is.False);
            Assert.That(viewModel.Networks, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task InitializeAsync_WithNoNetwork_HidesTheNetworkSelector()
    {
        A.CallTo(() => _networkAddressProvider.GetAvailableAddresses())
            .Returns(new List<NetworkAddressOption>());

        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync();

        Assert.That(viewModel.IsNetworkSelectorVisible, Is.False);
    }

    [Test]
    public async Task InitializeAsync_WithSeveralNetworks_ShowsThemInTheSelector()
    {
        A.CallTo(() => _networkAddressProvider.GetAvailableAddresses())
            .Returns(new List<NetworkAddressOption> { new("WiFi", "192.168.1.20"), new("Festival", "10.0.0.5") });

        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsNetworkSelectorVisible, Is.True);
            Assert.That(viewModel.Networks, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task InitializeAsync_CalledTwice_KeepsTheSameNetworkCollectionInstance()
    {
        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync();
        object first = viewModel.Networks;

        await viewModel.InitializeAsync();

        Assert.That(viewModel.Networks, Is.SameAs(first));
    }

    [Test]
    public async Task Save_WithAPortOutsideTheAllowedRange_IsRefused()
    {
        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync();

        viewModel.Port = 70000;

        Assert.That(viewModel.CanSave, Is.False);
        viewModel.Save();
        A.CallTo(() => _settingsStore.Save(A<DesktopSettings>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task Save_WithAnEmptyBindAddress_IsRefused()
    {
        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync();

        viewModel.BindAddress = "   ";

        Assert.That(viewModel.CanSave, Is.False);
        viewModel.Save();
        A.CallTo(() => _settingsStore.Save(A<DesktopSettings>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task Save_WithValidValues_WritesThemToTheStore()
    {
        SettingsWindowViewModel viewModel = await CreateOpenedViewModelAsync();

        viewModel.Port = 8080;

        Assert.That(viewModel.CanSave, Is.True);
        viewModel.Save();
        A.CallTo(() => _settingsStore.Save(new DesktopSettings(8080, "0.0.0.0", DataFolder, null, null)))
            .MustHaveHappenedOnceExactly();
    }
}
