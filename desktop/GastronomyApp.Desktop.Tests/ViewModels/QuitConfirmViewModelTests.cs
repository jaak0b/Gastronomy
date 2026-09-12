using FakeItEasy;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Services;
using GastronomyApp.Desktop.ViewModels;

namespace GastronomyApp.Desktop.Tests.ViewModels;

[TestFixture]
public sealed class QuitConfirmViewModelTests
{

  [SetUp]
  public void SetUp()
  {
    _launcher = A.Fake<IHostLauncher>();
    _power = A.Fake<IPowerManager>();
    _text = new DesktopTextProvider();
    _exitRequests = 0;
  }

  private IHostLauncher _launcher = null!;
  private IPowerManager _power = null!;
  private IDesktopTextProvider _text = null!;
  private int _exitRequests;

  private MainWindowViewModel CreateHostViewModel()
  {
    var settingsStore = A.Fake<ISettingsStore>();
    A.CallTo(() => settingsStore.Load())
     .Returns(new(5000, @"C:\ProgramData\GastronomyApp", null, null));

    return new(_launcher,
               _power,
               settingsStore,
               _text,
               A.Fake<IFreePortProvider>(),
               A.Fake<IUpdateInstaller>(),
               "1.2.3");
  }

  private QuitConfirmViewModel CreateViewModel()
  {
    return new(CreateHostViewModel().StopAsync, _text, () => _exitRequests++, _ => Task.CompletedTask);
  }

  [Test]
  public void RequestQuit_OpensTheConfirmationAndStopsNothingYet()
  {
    var viewModel = CreateViewModel();
    Assert.That(viewModel.IsConfirmationVisible, Is.False);

    viewModel.RequestQuit();

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.IsConfirmationVisible, Is.True);
                      Assert.That(viewModel.Title, Is.EqualTo(_text.Get("desktop.quit.title")));
                      Assert.That(viewModel.Body, Is.EqualTo(_text.Get("desktop.quit.body")));
                      Assert.That(viewModel.ConfirmLabel, Is.EqualTo(_text.Get("desktop.quit.confirm")));
                      Assert.That(viewModel.CancelLabel, Is.EqualTo(_text.Get("desktop.quit.cancel")));
                      Assert.That(_exitRequests, Is.Zero);
                    });
    A.CallTo(() => _launcher.StopAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public void Cancel_ClosesTheConfirmationAndLeavesTheServerRunning()
  {
    var viewModel = CreateViewModel();
    viewModel.RequestQuit();

    viewModel.Cancel();

    Assert.Multiple(() =>
                    {
                      Assert.That(viewModel.IsConfirmationVisible, Is.False);
                      Assert.That(_exitRequests, Is.Zero);
                    });
    A.CallTo(() => _launcher.StopAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task ConfirmAsync_StopsTheServerBeforeAskingTheApplicationToExit()
  {
    var viewModel = CreateViewModel();
    var exitsSeenWhenStopping = -1;
    A.CallTo(() => _launcher.StopAsync(A<CancellationToken>._))
     .Invokes(() => exitsSeenWhenStopping = _exitRequests)
     .Returns(Task.CompletedTask);
    viewModel.RequestQuit();

    await viewModel.ConfirmAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(exitsSeenWhenStopping, Is.Zero);
                      Assert.That(_exitRequests, Is.EqualTo(1));
                      Assert.That(viewModel.IsConfirmationVisible, Is.False);
                    });
    A.CallTo(() => _launcher.StopAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task ConfirmAsync_ReleasesTheAwakeRequestWhileStoppingTheServer()
  {
    var host = CreateHostViewModel();
    QuitConfirmViewModel viewModel = new(host.StopAsync, _text, () => _exitRequests++, _ => Task.CompletedTask);
    viewModel.RequestQuit();

    await viewModel.ConfirmAsync();

    A.CallTo(() => _launcher.StopAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _power.AllowSleep()).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task ConfirmAsync_WithoutRequestQuit_NeverExitsTheApplication()
  {
    var viewModel = CreateViewModel();

    await viewModel.ConfirmAsync();

    Assert.That(_exitRequests, Is.Zero);
    A.CallTo(() => _launcher.StopAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task ConfirmAsync_PreparesTheUpdateBeforeStoppingTheServer()
  {
    List<string> order = [];
    QuitConfirmViewModel viewModel = new(_ =>
                                         {
                                           order.Add("stop");
                                           return Task.CompletedTask;
                                         },
                                         _text,
                                         () => _exitRequests++,
                                         _ =>
                                         {
                                           order.Add("update");
                                           return Task.CompletedTask;
                                         });
    viewModel.RequestQuit();

    await viewModel.ConfirmAsync();

    Assert.That(order, Is.EqualTo(new List<string> { "update", "stop" }));
  }
}
