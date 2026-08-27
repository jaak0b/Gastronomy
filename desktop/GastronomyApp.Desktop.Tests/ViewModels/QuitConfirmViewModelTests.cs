using FakeItEasy;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Services;
using GastronomyApp.Desktop.ViewModels;

namespace GastronomyApp.Desktop.Tests.ViewModels;

[TestFixture]
public sealed class QuitConfirmViewModelTests
{
    private IHostLauncher _launcher = null!;
    private IPowerManager _power = null!;
    private IDesktopTextProvider _text = null!;
    private int _exitRequests;

    [SetUp]
    public void SetUp()
    {
        _launcher = A.Fake<IHostLauncher>();
        _power = A.Fake<IPowerManager>();
        _text = new DesktopTextProvider();
        _exitRequests = 0;
    }

    private MainWindowViewModel CreateHostViewModel()
    {
        ISettingsStore settingsStore = A.Fake<ISettingsStore>();
        A.CallTo(() => settingsStore.Load())
            .Returns(new DesktopSettings(5000, "0.0.0.0", @"C:\ProgramData\GastronomyApp", null, null));

        return new MainWindowViewModel(
            _launcher,
            _power,
            settingsStore,
            _text);
    }

    private QuitConfirmViewModel CreateViewModel()
    {
        return new QuitConfirmViewModel(CreateHostViewModel().StopAsync, _text, () => _exitRequests++);
    }

    [Test]
    public void RequestQuit_OpensTheConfirmationAndStopsNothingYet()
    {
        QuitConfirmViewModel viewModel = CreateViewModel();
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
        QuitConfirmViewModel viewModel = CreateViewModel();
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
        QuitConfirmViewModel viewModel = CreateViewModel();
        int exitsSeenWhenStopping = -1;
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
        MainWindowViewModel host = CreateHostViewModel();
        QuitConfirmViewModel viewModel = new(host.StopAsync, _text, () => _exitRequests++);
        viewModel.RequestQuit();

        await viewModel.ConfirmAsync();

        A.CallTo(() => _launcher.StopAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _power.AllowSleep()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ConfirmAsync_WithoutRequestQuit_NeverExitsTheApplication()
    {
        QuitConfirmViewModel viewModel = CreateViewModel();

        await viewModel.ConfirmAsync();

        Assert.That(_exitRequests, Is.Zero);
        A.CallTo(() => _launcher.StopAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }
}
