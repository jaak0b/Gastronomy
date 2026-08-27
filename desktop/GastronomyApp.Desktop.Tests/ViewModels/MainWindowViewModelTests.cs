using FakeItEasy;
using GastronomyApp.Api.Options;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Services;
using GastronomyApp.Desktop.ViewModels;

namespace GastronomyApp.Desktop.Tests.ViewModels;

[TestFixture]
public sealed class MainWindowViewModelTests
{
    private IHostLauncher _launcher = null!;
    private IPowerManager _power = null!;
    private IQrCodeGenerator _qrCodeGenerator = null!;
    private INetworkAddressProvider _networkAddressProvider = null!;
    private ISettingsStore _settingsStore = null!;
    private IDesktopTextProvider _text = null!;

    [SetUp]
    public void SetUp()
    {
        _launcher = A.Fake<IHostLauncher>();
        _power = A.Fake<IPowerManager>();
        _qrCodeGenerator = A.Fake<IQrCodeGenerator>();
        _networkAddressProvider = A.Fake<INetworkAddressProvider>();
        _settingsStore = A.Fake<ISettingsStore>();
        _text = new DesktopTextProvider();

        A.CallTo(() => _settingsStore.Load())
            .Returns(new DesktopSettings(5000, "0.0.0.0", DataFolder, null));
        A.CallTo(() => _networkAddressProvider.GetAvailableAddresses())
            .Returns(new List<NetworkAddressOption> { new("WiFi", "192.168.1.20") });
        A.CallTo(() => _qrCodeGenerator.GenerateMatrix(A<string>._))
            .Returns(new List<bool[]> { new[] { true, false }, new[] { false, true } });
    }

    private const string DataFolder = @"C:\ProgramData\GastronomyApp";

    private MainWindowViewModel CreateViewModel()
    {
        return new MainWindowViewModel(
            _launcher,
            _power,
            _qrCodeGenerator,
            _networkAddressProvider,
            _settingsStore,
            _text);
    }

    private void LauncherReturns(HostLaunchResult result)
    {
        A.CallTo(() => _launcher.StartAsync(A<ApiHostOptions>._, A<CancellationToken>._)).Returns(result);
    }

    [Test]
    public async Task StartAsync_WhenTheHostStarts_TurnsRunningAndKeepsTheLaptopAwake()
    {
        LauncherReturns(new HostLaunchResult.Started(null!));
        MainWindowViewModel viewModel = CreateViewModel();
        Assert.That(viewModel.Status, Is.EqualTo(HostStatus.Stopped));

        await viewModel.StartAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Status, Is.EqualTo(HostStatus.Running));
            Assert.That(viewModel.StatusText, Is.EqualTo(_text.Get("desktop.status.running")));
            Assert.That(viewModel.ErrorMessageKey, Is.Null);
        });
        A.CallTo(() => _power.PreventSleep()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task StartAsync_WhenThePortIsTaken_ShowsThePortInUseTextAndStaysStopped()
    {
        LauncherReturns(new HostLaunchResult.PortInUse(5000));
        MainWindowViewModel viewModel = CreateViewModel();

        await viewModel.StartAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ErrorMessageKey, Is.EqualTo("desktop.error.portInUse"));
            Assert.That(viewModel.ErrorMessage, Does.Contain("5000"));
            Assert.That(viewModel.ErrorMessage, Does.Not.Contain("{port}"));
            Assert.That(viewModel.Status, Is.EqualTo(HostStatus.Stopped));
        });
        A.CallTo(() => _power.PreventSleep()).MustNotHaveHappened();
    }

    [Test]
    public async Task StartAsync_WhenTheDataFolderCannotBeWritten_ShowsTheRepairTextWithThePath()
    {
        LauncherReturns(new HostLaunchResult.DataFolderNotWritable(DataFolder));
        MainWindowViewModel viewModel = CreateViewModel();

        await viewModel.StartAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ErrorMessageKey, Is.EqualTo("desktop.error.dataFolderRepair"));
            Assert.That(viewModel.ErrorMessage, Does.Contain(DataFolder));
            Assert.That(viewModel.Status, Is.EqualTo(HostStatus.Stopped));
        });
    }

    [Test]
    public async Task StartAsync_WhenThereIsNoNetwork_ShowsTheNoNetworkText()
    {
        LauncherReturns(new HostLaunchResult.NoNetworkAvailable());
        MainWindowViewModel viewModel = CreateViewModel();

        await viewModel.StartAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ErrorMessageKey, Is.EqualTo("desktop.error.noNetwork"));
            Assert.That(viewModel.ErrorMessage, Is.EqualTo(_text.Get("desktop.error.noNetwork")));
            Assert.That(viewModel.Status, Is.EqualTo(HostStatus.Stopped));
        });
    }

    [Test]
    public async Task StartAsync_WhenStartingFailsForAnyOtherReason_ShowsAnErrorInsteadOfThrowing()
    {
        LauncherReturns(new HostLaunchResult.StartFailed(new InvalidOperationException("broken")));
        MainWindowViewModel viewModel = CreateViewModel();

        await viewModel.StartAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ErrorMessageKey, Is.EqualTo("desktop.error.startFailed"));
            Assert.That(viewModel.ErrorMessage, Is.EqualTo(_text.Get("desktop.error.startFailed")));
            Assert.That(viewModel.Status, Is.EqualTo(HostStatus.Stopped));
        });
    }

    [Test]
    public async Task StopAsync_WhenRunning_TurnsStoppedAndReleasesTheAwakeRequest()
    {
        LauncherReturns(new HostLaunchResult.Started(null!));
        MainWindowViewModel viewModel = CreateViewModel();
        await viewModel.StartAsync();

        await viewModel.StopAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Status, Is.EqualTo(HostStatus.Stopped));
            Assert.That(viewModel.StatusText, Is.EqualTo(_text.Get("desktop.status.stopped")));
        });
        A.CallTo(() => _launcher.StopAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _power.AllowSleep()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public void Attention_WithNothingWaiting_IsNoneAndNothingElse()
    {
        MainWindowViewModel viewModel = CreateViewModel();

        viewModel.HasAttention = false;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Attention, Is.EqualTo(AttentionState.None));
            Assert.That(viewModel.AttentionTextKey, Is.EqualTo("desktop.attention.none"));
            Assert.That(viewModel.AttentionText, Is.EqualTo(_text.Get("desktop.attention.none")));
        });
    }

    [Test]
    public void Attention_WithSomethingWaiting_IsSomeAndNothingElse()
    {
        MainWindowViewModel viewModel = CreateViewModel();

        viewModel.HasAttention = true;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Attention, Is.EqualTo(AttentionState.Some));
            Assert.That(viewModel.AttentionTextKey, Is.EqualTo("desktop.attention.some"));
            Assert.That(viewModel.AttentionText, Is.EqualTo(_text.Get("desktop.attention.some")));
        });
    }

    [Test]
    public void PhoneCount_WhenNoPhoneHasConnected_UsesTheNoneText()
    {
        MainWindowViewModel viewModel = CreateViewModel();

        viewModel.PhoneCount = 0;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.PhonesTextKey, Is.EqualTo("desktop.phones.none"));
            Assert.That(viewModel.PhonesText, Is.EqualTo(_text.Get("desktop.phones.none")));
        });
    }

    [Test]
    public void PhoneCount_WhenOnePhoneIsSetUp_UsesTheSingularText()
    {
        MainWindowViewModel viewModel = CreateViewModel();

        viewModel.PhoneCount = 1;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.PhonesTextKey, Is.EqualTo("desktop.phones.one"));
            Assert.That(viewModel.PhonesText, Is.EqualTo(_text.Get("desktop.phones.one")));
        });
    }

    [Test]
    public void PhoneCount_WhenSeveralPhonesAreSetUp_BindsTheCountIntoThePluralText()
    {
        MainWindowViewModel viewModel = CreateViewModel();

        viewModel.PhoneCount = 4;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.PhonesTextKey, Is.EqualTo("desktop.phones.many"));
            Assert.That(viewModel.PhonesText, Does.Contain("4"));
            Assert.That(viewModel.PhonesText, Does.Not.Contain("{count}"));
        });
    }

    [Test]
    public void RefreshAddress_PassesTheAddressToTheEncoderUnchanged()
    {
        MainWindowViewModel viewModel = CreateViewModel();

        viewModel.RefreshAddress();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.AddressUrl, Is.EqualTo("http://192.168.1.20:5000"));
            Assert.That(viewModel.QrContent, Is.EqualTo(viewModel.AddressUrl));
            Assert.That(viewModel.QrMatrix, Has.Count.EqualTo(2));
        });
        A.CallTo(() => _qrCodeGenerator.GenerateMatrix("http://192.168.1.20:5000"))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public void RefreshAddress_WhenTheLaptopIsOnSeveralNetworks_UsesTheSelectedOne()
    {
        A.CallTo(() => _settingsStore.Load())
            .Returns(new DesktopSettings(5000, "0.0.0.0", DataFolder, "Festival"));
        A.CallTo(() => _networkAddressProvider.GetAvailableAddresses())
            .Returns(new List<NetworkAddressOption> { new("WiFi", "192.168.1.20"), new("Festival", "10.0.0.5") });
        MainWindowViewModel viewModel = CreateViewModel();

        viewModel.RefreshAddress();

        Assert.That(viewModel.AddressUrl, Is.EqualTo("http://10.0.0.5:5000"));
    }
}
