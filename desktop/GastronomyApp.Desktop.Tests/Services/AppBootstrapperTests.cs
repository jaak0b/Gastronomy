using FakeItEasy;
using GastronomyApp.Api.Options;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Services;
using GastronomyApp.Desktop.Tests.Logging;
using GastronomyApp.Desktop.ViewModels;
using Serilog.Events;

namespace GastronomyApp.Desktop.Tests.Services;

[TestFixture]
public sealed class AppBootstrapperTests
{

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

  private ISingleInstance _singleInstance = null!;
  private IHostLauncher _launcher = null!;
  private int _viewModelsBuilt;
  private int _windowsBroughtToFront;
  private int _dispatchedToUi;
  private bool _dispatchImmediately;

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

    var settingsStore = A.Fake<ISettingsStore>();
    A.CallTo(() => settingsStore.Load())
     .Returns(new(5000, @"C:\ProgramData\GastronomyApp", null, null));

    return new(_launcher,
               A.Fake<IPowerManager>(),
               settingsStore,
               new DesktopTextProvider(),
               A.Fake<IFreePortProvider>(),
               A.Fake<IUpdateInstaller>(),
               "1.2.3");
  }

  private AppBootstrapper CreateBootstrapper()
  {
    return new(_singleInstance,
               BuildViewModel,
               () => _windowsBroughtToFront++,
               Dispatch);
  }

  [Test]
  public void Start_WhenAnotherInstanceAlreadyServes_ExitsWithoutBuildingAnythingOrStartingTheServer()
  {
    A.CallTo(() => _singleInstance.AcquireOrSignalExisting())
     .Returns(SingleInstanceOutcome.SignaledExistingAndShouldExit);
    var bootstrapper = CreateBootstrapper();

    var outcome = bootstrapper.Start();

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome, Is.EqualTo(BootstrapOutcome.ExitImmediately));
                      Assert.That(_viewModelsBuilt, Is.Zero);
                      Assert.That(bootstrapper.MainWindowViewModel, Is.Null);
                    });
    A.CallTo(() => _launcher.StartAsync(A<ApiHostOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _singleInstance.StartListeningForActivation()).MustNotHaveHappened();
  }

  [Test]
  public void Start_WhenItIsTheFirstInstance_BuildsTheWindowViewModelOnce()
  {
    A.CallTo(() => _singleInstance.AcquireOrSignalExisting())
     .Returns(SingleInstanceOutcome.AcquiredPrimary);
    var bootstrapper = CreateBootstrapper();

    var outcome = bootstrapper.Start();

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome, Is.EqualTo(BootstrapOutcome.ProceedToWindow));
                      Assert.That(_viewModelsBuilt, Is.EqualTo(1));
                      Assert.That(bootstrapper.MainWindowViewModel, Is.Not.Null);
                    });
    A.CallTo(() => _singleInstance.AcquireOrSignalExisting())
     .MustHaveHappened()
     .Then(A.CallTo(() => _singleInstance.StartListeningForActivation()).MustHaveHappenedOnceExactly());
  }

  [Test]
  public void ActivationRequested_AfterASecondLaunchSignals_BringsTheExistingWindowToFrontAndOpensNoSecondOne()
  {
    A.CallTo(() => _singleInstance.AcquireOrSignalExisting())
     .Returns(SingleInstanceOutcome.AcquiredPrimary);
    var bootstrapper = CreateBootstrapper();
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
    var bootstrapper = CreateBootstrapper();
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
  public void Start_WhenTakingTheInstanceFails_ShowsTheWindowWithAnErrorAndStartsNoServer()
  {
    A.CallTo(() => _singleInstance.AcquireOrSignalExisting())
     .Throws(new IOException("The named pipe could not be reached."));
    var bootstrapper = CreateBootstrapper();

    var outcome = bootstrapper.Start();

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome, Is.EqualTo(BootstrapOutcome.ProceedToWindowWithoutServer));
                      Assert.That(bootstrapper.MainWindowViewModel, Is.Not.Null);
                      Assert.That(bootstrapper.MainWindowViewModel!.HasError, Is.True);
                      Assert.That(bootstrapper.MainWindowViewModel.ErrorMessageKey,
                                  Is.EqualTo("desktop.error.instanceCheckFailed"));
                    });
    A.CallTo(() => _launcher.StartAsync(A<ApiHostOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public void Start_WhenTakingTheInstanceFails_TellsTheOperatorWhatToDoInPlainWords()
  {
    A.CallTo(() => _singleInstance.AcquireOrSignalExisting())
     .Throws(new IOException("The named pipe could not be reached."));
    var bootstrapper = CreateBootstrapper();

    bootstrapper.Start();

    Assert.That(bootstrapper.MainWindowViewModel!.ErrorMessage,
                Is.EqualTo("Look on the task bar for a window of this program. If there is none, "
                           + "restart the laptop and open the program again. The program could not "
                           + "check whether it is already running and has not started the server."));
  }

  [Test]
  public void Start_WhenTakingTheInstanceFails_WritesTheFailureToTheLogWithTheReason()
  {
    using RecordedLog log = new();
    IOException failure = new("The named pipe could not be reached.");
    A.CallTo(() => _singleInstance.AcquireOrSignalExisting()).Throws(failure);
    var bootstrapper = CreateBootstrapper();

    bootstrapper.Start();

    Assert.That(log.Entries.Where(entry => entry.Level == LogEventLevel.Error
                                           && ReferenceEquals(entry.Exception, failure)),
                Is.Not.Empty);
  }

  [Test]
  public void ActivationRequested_BeforeTheInstanceWasAcquired_BringsNothingToFront()
  {
    A.CallTo(() => _singleInstance.AcquireOrSignalExisting())
     .Returns(SingleInstanceOutcome.SignaledExistingAndShouldExit);
    var bootstrapper = CreateBootstrapper();
    bootstrapper.Start();

    _singleInstance.ActivationRequested += Raise.FreeForm<Action>.With();

    Assert.That(_windowsBroughtToFront, Is.Zero);
  }
}
