using FakeItEasy;
using GastronomyApp.Api.Options;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Services;
using GastronomyApp.Desktop.ViewModels;

namespace GastronomyApp.Desktop.Tests.Services;

[TestFixture]
public sealed class AppBootstrapperTests
{
    private ISingleInstance _singleInstance = null!;
    private IHostLauncher _launcher = null!;
    private int _viewModelsBuilt;
    private int _windowsBroughtToFront;
    private int _dispatchedToUi;
    private bool _dispatchImmediately;

    [SetUp]
    public void SetUp()
    {
        _singleInstance = A.Fake<ISingleInstance>();
        _launcher = A.Fake<IHostLauncher>();
        _viewModelsBuilt = 0;
        _windowsBroughtToFront = 0;
        _dispatchedToUi = 0;
        _dispatchImmediately = true;
    }

    private void Dispatch(Action work)
    {
        _dispatchedToUi++;

        if (_dispatchImmediately)
        {
            work();
        }
    }

    private MainWindowViewModel BuildViewModel()
    {
        _viewModelsBuilt++;

        ISettingsStore settingsStore = A.Fake<ISettingsStore>();
        A.CallTo(() => settingsStore.Load())
            .Returns(new DesktopSettings(5000, @"C:\ProgramData\GastronomyApp", null, null));

        return new MainWindowViewModel(
            _launcher,
            A.Fake<IPowerManager>(),
            settingsStore,
            new DesktopTextProvider(),
            A.Fake<IFreePortProvider>());
    }

    private AppBootstrapper CreateBootstrapper()
    {
        return new AppBootstrapper(
            _singleInstance,
            BuildViewModel,
            () => _windowsBroughtToFront++,
            Dispatch);
    }

    [Test]
    public void Start_WhenAnotherInstanceAlreadyServes_ExitsWithoutBuildingAnythingOrStartingTheServer()
    {
        A.CallTo(() => _singleInstance.AcquireOrSignalExisting())
            .Returns(SingleInstanceOutcome.SignaledExistingAndShouldExit);
        AppBootstrapper bootstrapper = CreateBootstrapper();

        BootstrapOutcome outcome = bootstrapper.Start();

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(BootstrapOutcome.ExitImmediately));
            Assert.That(_viewModelsBuilt, Is.Zero);
            Assert.That(bootstrapper.MainWindowViewModel, Is.Null);
        });
        A.CallTo(() => _launcher.StartAsync(A<ApiHostOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public void Start_WhenItIsTheFirstInstance_BuildsTheWindowViewModelOnce()
    {
        A.CallTo(() => _singleInstance.AcquireOrSignalExisting())
            .Returns(SingleInstanceOutcome.AcquiredPrimary);
        AppBootstrapper bootstrapper = CreateBootstrapper();

        BootstrapOutcome outcome = bootstrapper.Start();

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(BootstrapOutcome.ProceedToWindow));
            Assert.That(_viewModelsBuilt, Is.EqualTo(1));
            Assert.That(bootstrapper.MainWindowViewModel, Is.Not.Null);
        });
    }

    [Test]
    public void ActivationRequested_AfterASecondLaunchSignals_BringsTheExistingWindowToFrontAndOpensNoSecondOne()
    {
        A.CallTo(() => _singleInstance.AcquireOrSignalExisting())
            .Returns(SingleInstanceOutcome.AcquiredPrimary);
        AppBootstrapper bootstrapper = CreateBootstrapper();
        bootstrapper.Start();

        _singleInstance.ActivationRequested += Raise.FreeForm<Action>.With();

        Assert.Multiple(() =>
        {
            Assert.That(_windowsBroughtToFront, Is.EqualTo(1));
            Assert.That(_viewModelsBuilt, Is.EqualTo(1));
        });
    }

    [Test]
    public void ActivationRequested_BringsTheWindowToFrontOnlyThroughTheUiDispatcher()
    {
        A.CallTo(() => _singleInstance.AcquireOrSignalExisting())
            .Returns(SingleInstanceOutcome.AcquiredPrimary);
        AppBootstrapper bootstrapper = CreateBootstrapper();
        bootstrapper.Start();
        _dispatchImmediately = false;

        _singleInstance.ActivationRequested += Raise.FreeForm<Action>.With();

        Assert.Multiple(() =>
        {
            Assert.That(_dispatchedToUi, Is.EqualTo(1));
            Assert.That(_windowsBroughtToFront, Is.Zero);
        });
    }

    [Test]
    public void Start_WhenTakingTheInstanceFails_ExitsRatherThanRiskingASecondServer()
    {
        A.CallTo(() => _singleInstance.AcquireOrSignalExisting())
            .Throws(new IOException("The named pipe could not be reached."));
        AppBootstrapper bootstrapper = CreateBootstrapper();

        BootstrapOutcome outcome = bootstrapper.Start();

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(BootstrapOutcome.ExitImmediately));
            Assert.That(_viewModelsBuilt, Is.Zero);
        });
    }

    [Test]
    public void ActivationRequested_BeforeTheInstanceWasAcquired_BringsNothingToFront()
    {
        A.CallTo(() => _singleInstance.AcquireOrSignalExisting())
            .Returns(SingleInstanceOutcome.SignaledExistingAndShouldExit);
        AppBootstrapper bootstrapper = CreateBootstrapper();
        bootstrapper.Start();

        _singleInstance.ActivationRequested += Raise.FreeForm<Action>.With();

        Assert.That(_windowsBroughtToFront, Is.Zero);
    }
}
