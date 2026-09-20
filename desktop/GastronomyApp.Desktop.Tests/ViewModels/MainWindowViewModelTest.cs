using System.Globalization;
using FakeItEasy;
using GastronomyApp.Api.Options;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Services;
using GastronomyApp.Desktop.ViewModels;

namespace GastronomyApp.Desktop.Tests.ViewModels;

[TestFixture]
public sealed class MainWindowViewModelTests
{

  [SetUp]
  public void SetUp()
  {
    _launcher = A.Fake<IHostLauncher>();
    _power = A.Fake<IPowerManager>();
    _settingsStore = A.Fake<ISettingsStore>();
    _text = new DesktopTextProvider();
    _freePorts = A.Fake<IFreePortProvider>();
    _updateInstaller = A.Fake<IUpdateInstaller>();
    A.CallTo(() => _freePorts.Reserve()).Returns(51234);

    A.CallTo(() => _settingsStore.Load())
     .Returns(new(5000, DataFolder, null, null));
  }

  private IHostLauncher _launcher = null!;
  private IPowerManager _power = null!;
  private ISettingsStore _settingsStore = null!;
  private IDesktopTextProvider _text = null!;
  private IFreePortProvider _freePorts = null!;
  private IUpdateInstaller _updateInstaller = null!;

  private const string DataFolder = @"C:\ProgramData\GastronomyApp";
  private const string CurrentVersion = "1.2.3";

  private MainWindowViewModel CreateViewModel(IUpdateInstaller? updateInstaller = null)
  {
    return new(_launcher,
               _power,
               _settingsStore,
               _text,
               _freePorts,
               updateInstaller ?? _updateInstaller,
               CurrentVersion);
  }

  private void LauncherReturns(HostLaunchResult result)
  {
    A.CallTo(() => _launcher.StartAsync(A<ApiHostOptions>._, A<CancellationToken>._)).Returns(result);
  }

  [Test]
  public async Task StartAsync_WhenTheHostStarts_TurnsRunningAndKeepsTheLaptopAwake()
  {
    LauncherReturns(new HostLaunchResult.Started(null!));
    var viewModel = CreateViewModel();
    Assert.That(viewModel.Status, Is.EqualTo(HostStatus.Stopped));

    await viewModel.StartAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.Status, Is.EqualTo(HostStatus.Running));
                      Assert.That(viewModel.ErrorMessageKey, Is.Null);
                    });
    A.CallTo(() => _power.PreventSleep()).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task StartAsync_WhenTheDataFolderCannotBeWritten_ShowsTheRepairTextWithThePath()
  {
    LauncherReturns(new HostLaunchResult.DataFolderNotWritable(DataFolder));
    var viewModel = CreateViewModel();

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
    var viewModel = CreateViewModel();

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
    var viewModel = CreateViewModel();

    await viewModel.StartAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.ErrorMessageKey, Is.EqualTo("desktop.error.startFailed"));
                      Assert.That(viewModel.ErrorMessage, Is.EqualTo(_text.Get("desktop.error.startFailed")));
                      Assert.That(viewModel.Status, Is.EqualTo(HostStatus.Stopped));
                    });
  }

  [Test]
  public async Task StartAsync_WhenStartingFails_KeepsTheExceptionTextAndOffersToShowIt()
  {
    var failure = new InvalidOperationException("broken");
    LauncherReturns(new HostLaunchResult.StartFailed(failure));
    var viewModel = CreateViewModel();

    await viewModel.StartAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.FailureDetail, Is.EqualTo(failure.ToString()));
                      Assert.That(viewModel.CanShowFailureDetail, Is.True);
                    });
  }

  [Test]
  public async Task ShowFailureDetail_AfterAFailedStart_AsksForTheExceptionTextToBeShown()
  {
    LauncherReturns(new HostLaunchResult.StartFailed(new InvalidOperationException("broken")));
    var viewModel = CreateViewModel();
    await viewModel.StartAsync();
    var requests = 0;
    viewModel.FailureDetailRequested += () => requests++;

    viewModel.ShowFailureDetailCommand.Execute(null);

    Assert.Multiple(() =>
                    {
                      Assert.That(requests, Is.EqualTo(1));
                      Assert.That(viewModel.FailureDetail, Does.Contain("InvalidOperationException"));
                      Assert.That(viewModel.FailureDetail, Does.Contain("broken"));
                    });
  }

  [Test]
  public async Task ShowFailureDetail_WhenThereIsNoExceptionText_AsksForNothing()
  {
    LauncherReturns(new HostLaunchResult.NoNetworkAvailable());
    var viewModel = CreateViewModel();
    await viewModel.StartAsync();
    var requests = 0;
    viewModel.FailureDetailRequested += () => requests++;

    viewModel.ShowFailureDetailCommand.Execute(null);

    Assert.Multiple(() =>
                    {
                      Assert.That(requests, Is.Zero);
                      Assert.That(viewModel.FailureDetail, Is.Null);
                      Assert.That(viewModel.CanShowFailureDetail, Is.False);
                    });
  }

  [Test]
  public async Task StartAsync_WhenTheServerStartsAfterAFailedAttempt_ForgetsTheExceptionText()
  {
    LauncherReturns(new HostLaunchResult.StartFailed(new InvalidOperationException("broken")));
    var viewModel = CreateViewModel();
    await viewModel.StartAsync();
    LauncherReturns(new HostLaunchResult.Started(null!));

    await viewModel.StartAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.FailureDetail, Is.Null);
                      Assert.That(viewModel.CanShowFailureDetail, Is.False);
                    });
  }

  [Test]
  public async Task StopAsync_WhenRunning_TurnsStoppedAndReleasesTheAwakeRequest()
  {
    LauncherReturns(new HostLaunchResult.Started(null!));
    var viewModel = CreateViewModel();
    await viewModel.StartAsync();

    await viewModel.StopAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.Status, Is.EqualTo(HostStatus.Stopped));
                    });
    A.CallTo(() => _launcher.StopAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _power.AllowSleep()).MustHaveHappenedOnceExactly();
  }

  [Test]
  public void OpenAdminPagesCommand_OpensTheAdminPageOnLoopbackWithTheConfiguredPort()
  {
    A.CallTo(() => _settingsStore.Load())
     .Returns(new(8080, DataFolder, null, null));
    var viewModel = CreateViewModel();
    string? opened = null;
    viewModel.AdminPagesRequested += url => opened = url;

    viewModel.OpenAdminPagesCommand.Execute(null);

    Assert.That(opened, Is.EqualTo("http://localhost:8080/admin"));
  }

  [Test]
  public void OpenAdminPagesCommand_NeverOpensAnAddressFromTheNetwork()
  {
    var viewModel = CreateViewModel();
    string? opened = null;
    viewModel.AdminPagesRequested += url => opened = url;

    viewModel.OpenAdminPagesCommand.Execute(null);

    Assert.Multiple(() =>
                    {
                      Assert.That(opened, Does.Not.Contain("192.168."));
                      Assert.That(opened, Is.EqualTo("http://localhost:5000/admin"));
                    });
  }

  [Test]
  public void Languages_OffersDeutschAndEnglishAndNothingElse()
  {
    var viewModel = CreateViewModel();

    Assert.That(viewModel.Languages.Select(language => language.Name),
                Is.EqualTo(new List<string> { "Deutsch", "English" }));
  }

  [Test]
  public void SelectedLanguage_WithNoChoiceStored_FollowsWindows()
  {
    var original = CultureInfo.CurrentUICulture;
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

    try
    {
      var viewModel = CreateViewModel();

      Assert.That(viewModel.SelectedLanguage!.Code, Is.EqualTo("de"));
    } finally
    {
      CultureInfo.CurrentUICulture = original;
    }
  }

  [Test]
  public void SelectedLanguage_WithAStoredChoice_UsesTheStoredOne()
  {
    var original = CultureInfo.CurrentUICulture;
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
    A.CallTo(() => _settingsStore.Load())
     .Returns(new(5000, DataFolder, null, "de"));

    try
    {
      var viewModel = CreateViewModel();

      Assert.Multiple(() =>
                      {
                        Assert.That(viewModel.SelectedLanguage!.Code, Is.EqualTo("de"));
                        Assert.That(viewModel.AdminButtonLabel, Is.EqualTo("Verwaltung öffnen"));
                      });
    } finally
    {
      CultureInfo.CurrentUICulture = original;
    }
  }

  [Test]
  public void SelectedLanguage_WhenChanged_ChangesTheWindowTextAtOnce()
  {
    var original = CultureInfo.CurrentUICulture;
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

    try
    {
      var viewModel = CreateViewModel();
      List<string?> changedProperties = [];
      viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

      viewModel.SelectedLanguage = viewModel.Languages.Single(language => language.Code == "de");

      Assert.Multiple(() =>
                      {
                        Assert.That(viewModel.AdminButtonLabel, Is.EqualTo("Verwaltung öffnen"));
                        Assert.That(viewModel.QuitButtonLabel, Is.EqualTo("Programm beenden"));
                        Assert.That(changedProperties, Does.Contain(string.Empty));
                      });
    } finally
    {
      CultureInfo.CurrentUICulture = original;
    }
  }

  [Test]
  public void SelectedLanguage_WhenChanged_IsRememberedForTheNextStart()
  {
    var viewModel = CreateViewModel();

    viewModel.SelectedLanguage = viewModel.Languages.Single(language => language.Code == "de");

    A.CallTo(() => _settingsStore.Save(new(5000, DataFolder, null, "de")))
     .MustHaveHappenedOnceExactly();
  }


  [Test]
  public async Task StartAsync_WithNoPortWrittenDownYet_AsksWindowsForOneAndWritesItDown()
  {
    A.CallTo(() => _settingsStore.Load())
     .Returns(new(null, DataFolder, null, null));
    LauncherReturns(new HostLaunchResult.Started(null!));
    var viewModel = CreateViewModel();

    await viewModel.StartAsync();

    A.CallTo(() => _launcher.StartAsync(A<ApiHostOptions>.That.Matches(options => options.Port == 51234),
                                        A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _settingsStore.Save(A<DesktopSettings>.That.Matches(saved => saved.Port == 51234)))
     .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task StartAsync_WithAPortWrittenDownThatIsFree_UsesItAndSaysNothing()
  {
    LauncherReturns(new HostLaunchResult.Started(null!));
    var viewModel = CreateViewModel();

    await viewModel.StartAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.HasNotice, Is.False);
                      Assert.That(viewModel.ErrorMessageKey, Is.Null);
                    });
    A.CallTo(() => _launcher.StartAsync(A<ApiHostOptions>.That.Matches(options => options.Port == 5000),
                                        A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _freePorts.Reserve()).MustNotHaveHappened();
  }

  [Test]
  public async Task StartAsync_WhenTheWrittenDownPortIsTaken_MovesToAFreeOneAndWritesItDown()
  {
    A.CallTo(() => _launcher.StartAsync(A<ApiHostOptions>.That.Matches(options => options.Port == 5000),
                                        A<CancellationToken>._))
     .Returns(new HostLaunchResult.PortInUse(5000));
    A.CallTo(() => _launcher.StartAsync(A<ApiHostOptions>.That.Matches(options => options.Port == 51234),
                                        A<CancellationToken>._))
     .Returns(new HostLaunchResult.Started(null!));
    var viewModel = CreateViewModel();

    await viewModel.StartAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.Status, Is.EqualTo(HostStatus.Running));
                      Assert.That(viewModel.ErrorMessageKey, Is.Null);
                    });
    A.CallTo(() => _settingsStore.Save(A<DesktopSettings>.That.Matches(saved => saved.Port == 51234)))
     .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task StartAsync_WhenThePortChanged_TellsTheOperatorEverybodyMustSetTheirPhoneUpAgain()
  {
    A.CallTo(() => _launcher.StartAsync(A<ApiHostOptions>.That.Matches(options => options.Port == 5000),
                                        A<CancellationToken>._))
     .Returns(new HostLaunchResult.PortInUse(5000));
    A.CallTo(() => _launcher.StartAsync(A<ApiHostOptions>.That.Matches(options => options.Port == 51234),
                                        A<CancellationToken>._))
     .Returns(new HostLaunchResult.Started(null!));
    var viewModel = CreateViewModel();

    await viewModel.StartAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.HasNotice, Is.True);
                      Assert.That(viewModel.NoticeText, Is.EqualTo(_text.Get("desktop.notice.addressChanged")));
                    });
  }

  [Test]
  public async Task StartAsync_WhenEveryPortItTriesIsTaken_GivesUpAndSaysSo()
  {
    LauncherReturns(new HostLaunchResult.PortInUse(5000));
    var viewModel = CreateViewModel();

    await viewModel.StartAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.Status, Is.EqualTo(HostStatus.Stopped));
                      Assert.That(viewModel.ErrorMessageKey, Is.EqualTo("desktop.error.noPortAvailable"));
                    });
    A.CallTo(() => _launcher.StartAsync(A<ApiHostOptions>._, A<CancellationToken>._))
     .MustHaveHappened(10, Times.Exactly);
  }

  [Test]
  public async Task StartAsync_WhenTheFailureIsNotABusyPort_StopsAtOnceRatherThanLooping()
  {
    LauncherReturns(new HostLaunchResult.NoNetworkAvailable());
    var viewModel = CreateViewModel();

    await viewModel.StartAsync();

    A.CallTo(() => _launcher.StartAsync(A<ApiHostOptions>._, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _freePorts.Reserve()).MustNotHaveHappened();
  }

  [Test]
  public async Task StartAsync_Always_BindsEveryNetworkInterface()
  {
    LauncherReturns(new HostLaunchResult.Started(null!));
    var viewModel = CreateViewModel();

    await viewModel.StartAsync();

    A.CallTo(() => _launcher.StartAsync(A<ApiHostOptions>.That.Matches(options => options.BindAddress == "0.0.0.0"),
                                        A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Test]
  public void CurrentStatus_OnAWindowThatHasNotStartedTheServerYet_IsStarting()
  {
    var viewModel = CreateViewModel();

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.CurrentStatus, Is.EqualTo(StatusLevel.Starting));
                      Assert.That(viewModel.StatusText, Is.Null);
                    });
  }

  [Test]
  public async Task CurrentStatus_WhenTheServerAnswers_IsRunningAndSaysNothing()
  {
    LauncherReturns(new HostLaunchResult.Started(null!));
    var viewModel = CreateViewModel();

    await viewModel.StartAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.CurrentStatus, Is.EqualTo(StatusLevel.Running));
                      Assert.That(viewModel.StatusText, Is.Null);
                    });
  }

  [Test]
  public async Task CurrentStatus_WhenTheServerCannotStart_IsDownAndCarriesTheErrorText()
  {
    LauncherReturns(new HostLaunchResult.NoNetworkAvailable());
    var viewModel = CreateViewModel();

    await viewModel.StartAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.CurrentStatus, Is.EqualTo(StatusLevel.Down));
                      Assert.That(viewModel.StatusText, Is.EqualTo(_text.Get("desktop.error.noNetwork")));
                    });
  }

  [Test]
  public void CurrentStatus_WhenTheRepairWasDeclined_IsWarningAndCarriesTheDeclinedText()
  {
    var viewModel = CreateViewModel();

    viewModel.ShowRepairOutcome(ElevatedSetupOutcome.ElevationDeclined);

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.CurrentStatus, Is.EqualTo(StatusLevel.Warning));
                      Assert.That(viewModel.StatusText, Is.EqualTo(_text.Get("desktop.settings.repairDeclined")));
                    });
  }

  [Test]
  public void CurrentStatus_WhenAStepOfTheRepairFailed_IsWarningAndCarriesTheFailureText()
  {
    var viewModel = CreateViewModel();
    viewModel.SelectedLanguage = viewModel.Languages.Single(language => language.Code == "en");

    viewModel.ShowRepairOutcome(ElevatedSetupOutcome.SetupStepFailed);
    var english = viewModel.StatusText;
    var expectedEnglish = _text.Get("desktop.settings.repairFailed");

    viewModel.SelectedLanguage = viewModel.Languages.Single(language => language.Code == "de");
    var german = viewModel.StatusText;
    var expectedGerman = _text.Get("desktop.settings.repairFailed");

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.CurrentStatus, Is.EqualTo(StatusLevel.Warning));
                      Assert.That(english, Is.EqualTo(expectedEnglish));
                      Assert.That(german, Is.EqualTo(expectedGerman));
                      Assert.That(german, Is.Not.EqualTo(english));
                    });
  }

  [Test]
  public void CurrentStatus_WhenAStepOfTheFirstRunSetupFailed_IsWarningAndCarriesTheFailureText()
  {
    var viewModel = CreateViewModel();
    viewModel.SelectedLanguage = viewModel.Languages.Single(language => language.Code == "en");

    viewModel.ShowSetupFailed();
    var english = viewModel.StatusText;
    var expectedEnglish = _text.Get("desktop.firstRun.setupFailed");

    viewModel.SelectedLanguage = viewModel.Languages.Single(language => language.Code == "de");
    var german = viewModel.StatusText;
    var expectedGerman = _text.Get("desktop.firstRun.setupFailed");

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.CurrentStatus, Is.EqualTo(StatusLevel.Warning));
                      Assert.That(english, Is.EqualTo(expectedEnglish));
                      Assert.That(german, Is.EqualTo(expectedGerman));
                      Assert.That(german, Is.Not.EqualTo(english));
                    });
  }

  [Test]
  public async Task CurrentStatus_WhenTheRepairWasCompleted_StaysRunningAndCarriesTheDoneText()
  {
    LauncherReturns(new HostLaunchResult.Started(null!));
    var viewModel = CreateViewModel();
    await viewModel.StartAsync();

    viewModel.ShowRepairOutcome(ElevatedSetupOutcome.Completed);

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.CurrentStatus, Is.EqualTo(StatusLevel.Running));
                      Assert.That(viewModel.StatusText, Is.EqualTo(_text.Get("desktop.settings.repairDone")));
                    });
  }

  [Test]
  public async Task CurrentStatus_WhenTheAddressChanged_WarnsAndCarriesTheNoticeText()
  {
    A.CallTo(() => _launcher.StartAsync(A<ApiHostOptions>.That.Matches(options => options.Port == 5000),
                                        A<CancellationToken>._))
     .Returns(new HostLaunchResult.PortInUse(5000));
    A.CallTo(() => _launcher.StartAsync(A<ApiHostOptions>.That.Matches(options => options.Port == 51234),
                                        A<CancellationToken>._))
     .Returns(new HostLaunchResult.Started(null!));
    var viewModel = CreateViewModel();

    await viewModel.StartAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.CurrentStatus, Is.EqualTo(StatusLevel.Warning));
                      Assert.That(viewModel.StatusText, Is.EqualTo(_text.Get("desktop.notice.addressChanged")));
                    });
  }

  [Test]
  public async Task CurrentStatus_WhenAnErrorAndANoticeArriveTogether_ShowsTheError()
  {
    LauncherReturns(new HostLaunchResult.NoNetworkAvailable());
    var viewModel = CreateViewModel();
    viewModel.ShowRepairOutcome(ElevatedSetupOutcome.Completed);

    await viewModel.StartAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.CurrentStatus, Is.EqualTo(StatusLevel.Down));
                      Assert.That(viewModel.StatusText, Is.EqualTo(_text.Get("desktop.error.noNetwork")));
                    });
  }

  [Test]
  public void StatusText_OfANotice_WhenTheLanguageChanges_ChangesWithIt()
  {
    var viewModel = CreateViewModel();
    viewModel.SelectedLanguage = viewModel.Languages.Single(language => language.Code == "en");
    viewModel.ShowRepairOutcome(ElevatedSetupOutcome.ElevationDeclined);
    var english = viewModel.StatusText;

    viewModel.SelectedLanguage = viewModel.Languages.Single(language => language.Code == "de");

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.StatusText, Is.EqualTo(_text.Get("desktop.settings.repairDeclined")));
                      Assert.That(viewModel.StatusText, Is.Not.EqualTo(english));
                    });
  }

  [Test]
  public async Task StatusText_OfAnError_WhenTheLanguageChanges_ChangesWithIt()
  {
    LauncherReturns(new HostLaunchResult.NoNetworkAvailable());
    var viewModel = CreateViewModel();
    viewModel.SelectedLanguage = viewModel.Languages.Single(language => language.Code == "en");
    await viewModel.StartAsync();
    var english = viewModel.StatusText;

    viewModel.SelectedLanguage = viewModel.Languages.Single(language => language.Code == "de");

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.StatusText, Is.EqualTo(_text.Get("desktop.error.noNetwork")));
                      Assert.That(viewModel.StatusText, Is.Not.EqualTo(english));
                    });
  }

  [Test]
  public void VersionText_CarriesTheVersionOfTheRunningProgram()
  {
    var viewModel = CreateViewModel();

    Assert.That(viewModel.VersionText, Is.EqualTo("1.2.3"));
  }

  [Test]
  public void CanCheckForUpdates_WhenTheProgramIsInstalled_IsTrue()
  {
    A.CallTo(() => _updateInstaller.IsInstalled).Returns(true);

    Assert.That(CreateViewModel().CanCheckForUpdates, Is.True);
  }

  [Test]
  public void CanCheckForUpdates_WhenTheProgramIsNotInstalled_IsFalse()
  {
    A.CallTo(() => _updateInstaller.IsInstalled).Returns(false);

    Assert.That(CreateViewModel().CanCheckForUpdates, Is.False);
  }

  [Test]
  public async Task CheckForUpdatesCommand_WhenAnUpdateIsReady_AsksForTheConfirmationWithTheVersion()
  {
    var updateInstaller = A.Fake<IUpdateInstaller>();
    A.CallTo(() => updateInstaller.CheckAndDownloadAsync(A<CancellationToken>._))
     .Returns(new UpdatePreparation.Ready("2.0.0"));
    var viewModel = CreateViewModel(updateInstaller);
    string? requestedVersion = null;
    viewModel.UpdateReadyRequested += version => requestedVersion = version;

    await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

    Assert.That(requestedVersion, Is.EqualTo("2.0.0"));
  }

  [Test]
  public async Task CheckForUpdatesCommand_WhenTheCheckFails_AsksForTheTechnicalDetail()
  {
    var updateInstaller = A.Fake<IUpdateInstaller>();
    var failure = new InvalidOperationException("The network is down.");
    A.CallTo(() => updateInstaller.CheckAndDownloadAsync(A<CancellationToken>._))
     .Returns(new UpdatePreparation.Failed(failure));
    var viewModel = CreateViewModel(updateInstaller);
    Exception? requestedFailure = null;
    viewModel.UpdateFailureRequested += exception => requestedFailure = exception;

    await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

    Assert.That(requestedFailure, Is.SameAs(failure));
  }

  [Test]
  public async Task CheckForUpdatesCommand_WhileTheCheckRuns_ReportsThatItIsRunning()
  {
    var updateInstaller = A.Fake<IUpdateInstaller>();
    var pending = new TaskCompletionSource<UpdatePreparation>();
    A.CallTo(() => updateInstaller.CheckAndDownloadAsync(A<CancellationToken>._)).Returns(pending.Task);
    var viewModel = CreateViewModel(updateInstaller);

    var running = viewModel.CheckForUpdatesCommand.ExecuteAsync(null);
    var runningDuringCheck = viewModel.IsUpdateCheckRunning;

    pending.SetResult(new UpdatePreparation.UpToDate());
    await running;

    Assert.Multiple(() =>
                    {
                      Assert.That(runningDuringCheck, Is.True);
                      Assert.That(viewModel.IsUpdateCheckRunning, Is.False);
                    });
  }
}
