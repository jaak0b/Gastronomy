using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FakeItEasy;
using GastronomyApp.Api.Options;
using GastronomyApp.Desktop.Enums;
using GastronomyApp.Desktop.Hosting;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Platform;
using GastronomyApp.Desktop.Settings;
using GastronomyApp.Desktop.Tests.TestSupport;
using GastronomyApp.Desktop.Updates;
using GastronomyApp.Desktop.Values;
using GastronomyApp.Desktop.ViewModels;
using GastronomyApp.Desktop.Views;

namespace GastronomyApp.Desktop.Tests.Smoke;

[TestFixture]
public sealed class MainWindowSmokeTest
{
  private readonly IDesktopTextProvider _text = new DesktopTextProvider();
  private readonly RenderedColourReader _colours = new();

  private const string CurrentVersion = "1.2.3";

  private MainWindowViewModel CreateMainWindowViewModel(IHostLauncher? launcher = null, IUpdateInstaller? updateInstaller = null)
  {
    var settingsStore = A.Fake<ISettingsStore>();
    A.CallTo(() => settingsStore.Load()).Returns(new(5000, @"C:\ProgramData\GastronomyApp", null, null, null));

    return new(launcher ?? A.Fake<IHostLauncher>(), A.Fake<IPowerManager>(), settingsStore, _text, A.Fake<IFreePortProvider>(), updateInstaller ?? A.Fake<IUpdateInstaller>(), CurrentVersion);
  }

  private static IHostLauncher LauncherThat(HostLaunchResult result)
  {
    var launcher = A.Fake<IHostLauncher>();
    A.CallTo(() => launcher.StartAsync(A<ApiHostOptions>._, A<CancellationToken>._)).Returns(result);

    return launcher;
  }

  [AvaloniaTest]
  public void MainWindow_Loads_WithTheTitleResolvedFromTheResxTable()
  {
    var viewModel = CreateMainWindowViewModel();

    MainWindow window = new() { DataContext = viewModel };
    window.Show();

    Assert.That(window.Title, Is.EqualTo(_text.Get("desktop.windowTitle")));
  }

  [AvaloniaTest]
  public void MainWindow_BeforeTheServerHasAnswered_ShowsTheStatusBarInGreyWithoutText()
  {
    var viewModel = CreateMainWindowViewModel();

    MainWindow window = new() { DataContext = viewModel };
    window.Show();

    var statusBar = window.FindControl<Border>("StatusBar");
    var statusMessage = window.FindControl<TextBlock>("StatusMessage");

    Assert.Multiple(() =>
                    {
                      Assert.That(statusBar, Is.Not.Null);
                      Assert.That(_colours.ToColour(statusBar!.Background), Is.EqualTo(Color.Parse("#9CA3AF")));
                      Assert.That(statusMessage!.IsVisible, Is.False);
                    });
  }

  [AvaloniaTest]
  public async Task MainWindow_WhenTheServerAnswers_TurnsTheStatusBarGreen()
  {
    var viewModel = CreateMainWindowViewModel(LauncherThat(new HostLaunchResult.Started(null!)));

    MainWindow window = new() { DataContext = viewModel };
    window.Show();
    await viewModel.StartAsync();

    var statusBar = window.FindControl<Border>("StatusBar");

    Assert.Multiple(() =>
                    {
                      Assert.That(statusBar!.Classes, Does.Contain("running"));
                      Assert.That(_colours.ToColour(statusBar.Background), Is.EqualTo(Color.Parse("#2E8B57")));
                    });
  }

  [AvaloniaTest]
  public void MainWindow_WhenTheRepairWasDeclined_TurnsTheStatusBarToAWarningThatCarriesTheText()
  {
    var viewModel = CreateMainWindowViewModel();

    MainWindow window = new() { DataContext = viewModel };
    window.Show();
    viewModel.ShowRepairOutcome(ElevatedSetupOutcome.ElevationDeclined);

    var statusBar = window.FindControl<Border>("StatusBar");
    var statusMessage = window.FindControl<TextBlock>("StatusMessage");

    Assert.Multiple(() =>
                    {
                      Assert.That(statusBar!.Classes, Does.Contain("warning"));
                      Assert.That(statusMessage!.IsVisible, Is.True);
                      Assert.That(statusMessage.Text, Is.EqualTo(_text.Get("desktop.settings.repairDeclined")));
                    });
  }

  [AvaloniaTest]
  public async Task MainWindow_WhenTheServerCannotStart_TurnsTheStatusBarDownAndCarriesTheErrorText()
  {
    var viewModel = CreateMainWindowViewModel(LauncherThat(new HostLaunchResult.NoNetworkAvailable()));

    MainWindow window = new() { DataContext = viewModel };
    window.Show();
    await viewModel.StartAsync();

    var statusBar = window.FindControl<Border>("StatusBar");
    var statusMessage = window.FindControl<TextBlock>("StatusMessage");

    Assert.Multiple(() =>
                    {
                      Assert.That(statusBar!.Classes, Does.Contain("down"));
                      Assert.That(statusMessage!.Text, Is.EqualTo(_text.Get("desktop.error.noNetwork")));
                    });
  }

  [AvaloniaTest]
  public void MainWindow_WhenALongMessageFillsTheStatusBar_KeepsEveryButtonAtItsFullHeight()
  {
    var healthy = CreateMainWindowViewModel();
    MainWindow healthyWindow = new() { DataContext = healthy };
    healthyWindow.Show();
    Dispatcher.UIThread.RunJobs();
    var fullHeight = healthyWindow.FindControl<StackPanel>("Actions")!.Bounds.Height;

    var squeezed = CreateMainWindowViewModel();
    MainWindow squeezedWindow = new() { DataContext = squeezed };
    squeezedWindow.Height = squeezedWindow.MinHeight;
    squeezedWindow.Show();
    squeezed.ShowRepairOutcome(ElevatedSetupOutcome.ElevationDeclined);
    Dispatcher.UIThread.RunJobs();

    var squeezedHeight = squeezedWindow.FindControl<StackPanel>("Actions")!.Bounds.Height;

    Assert.Multiple(() =>
                    {
                      Assert.That(fullHeight, Is.GreaterThan(0));
                      Assert.That(squeezedHeight, Is.EqualTo(fullHeight));
                    });
  }

  private static IReadOnlyList<Button> ActionButtons(MainWindow window)
  {
    return window.FindControl<StackPanel>("Actions")!.GetVisualDescendants().OfType<Button>().ToList();
  }

  [AvaloniaTest]
  public void MainWindow_PaintsTheLabelOfEverySecondaryButtonInTheDarkTextColour()
  {
    var viewModel = CreateMainWindowViewModel();

    MainWindow window = new() { DataContext = viewModel };
    window.Show();
    Dispatcher.UIThread.RunJobs();

    List<Button> secondary = ActionButtons(window).Where(button => !button.Classes.Contains("accent")).ToList();

    Assert.Multiple(() =>
                    {
                      Assert.That(secondary, Has.Count.EqualTo(3));
                      Assert.That(secondary.Select(_colours.ReadLabelForeground), Is.All.EqualTo(Color.Parse("#1F2937")));
                      Assert.That(TextOptions.GetTextRenderingMode(window), Is.EqualTo(TextRenderingMode.Antialias));
                    });
  }

  [AvaloniaTest]
  public void MainWindow_PaintsTheLabelOfTheAdminButtonInWhiteOnTheAccentColour()
  {
    var viewModel = CreateMainWindowViewModel();

    MainWindow window = new() { DataContext = viewModel };
    window.Show();
    Dispatcher.UIThread.RunJobs();

    var accent = ActionButtons(window).Single(button => button.Classes.Contains("accent"));

    Assert.Multiple(() =>
                    {
                      Assert.That(_colours.ReadLabelForeground(accent), Is.EqualTo(Color.Parse("#FFFFFF")));
                      Assert.That(_colours.ReadLabelBackground(accent), Is.EqualTo(Color.Parse("#1F2937")));
                    });
  }

  [AvaloniaTest]
  public void MainWindow_ShowsTheVersionOfTheRunningProgram()
  {
    var viewModel = CreateMainWindowViewModel();

    MainWindow window = new() { DataContext = viewModel };
    window.Show();

    var versionText = window.FindControl<TextBlock>("VersionText");

    Assert.That(versionText!.Text, Is.EqualTo(_text.Format("desktop.version", new TextPlaceholder("version", CurrentVersion))));
  }

  [AvaloniaTest]
  public void MainWindow_RendersTheCustomTitleBarWithTheTitleTheVersionAndTheLanguagePicker()
  {
    var viewModel = CreateMainWindowViewModel();

    MainWindow window = new() { DataContext = viewModel };
    window.Show();

    var title = window.FindControl<TextBlock>("TitleBarTitle");
    var versionText = window.FindControl<TextBlock>("VersionText");
    var languageSelector = window.FindControl<ComboBox>("LanguageSelector");

    Assert.Multiple(() =>
                    {
                      Assert.That(title!.Text, Is.EqualTo(_text.Get("desktop.windowTitle")));
                      Assert.That(versionText!.Text, Is.EqualTo(CurrentVersion));
                      Assert.That(languageSelector, Is.Not.Null);
                      Assert.That(languageSelector!.ItemsSource, Is.SameAs(viewModel.Languages));
                    });
  }

  [AvaloniaTest]
  public void MainWindow_WithAnInstalledCopy_ShowsTheUpdateButton()
  {
    var updateInstaller = A.Fake<IUpdateInstaller>();
    A.CallTo(() => updateInstaller.IsInstalled).Returns(true);
    var viewModel = CreateMainWindowViewModel(updateInstaller: updateInstaller);

    MainWindow window = new() { DataContext = viewModel };
    window.Show();

    var button = window.FindControl<Button>("CheckForUpdates");

    Assert.That(button!.IsVisible, Is.True);
  }

  [AvaloniaTest]
  public void MainWindow_WithoutAnInstalledCopy_HidesTheUpdateButton()
  {
    var updateInstaller = A.Fake<IUpdateInstaller>();
    A.CallTo(() => updateInstaller.IsInstalled).Returns(false);
    var viewModel = CreateMainWindowViewModel(updateInstaller: updateInstaller);

    MainWindow window = new() { DataContext = viewModel };
    window.Show();

    var button = window.FindControl<Button>("CheckForUpdates");

    Assert.That(button!.IsVisible, Is.False);
  }

  [AvaloniaTest]
  public async Task MainWindow_WhileTheUpdateCheckRuns_ShowsTheSpinner()
  {
    var updateInstaller = A.Fake<IUpdateInstaller>();
    A.CallTo(() => updateInstaller.IsInstalled).Returns(true);
    TaskCompletionSource<UpdatePreparation> pending = new();
    A.CallTo(() => updateInstaller.CheckAndDownloadAsync(A<CancellationToken>._)).Returns(pending.Task);
    var viewModel = CreateMainWindowViewModel(updateInstaller: updateInstaller);

    MainWindow window = new() { DataContext = viewModel };
    window.Show();
    Dispatcher.UIThread.RunJobs();

    var spinner = window.FindControl<Ellipse>("UpdateSpinner");
    Assert.That(spinner!.IsVisible, Is.False);

    var running = viewModel.CheckForUpdatesCommand.ExecuteAsync(null);
    Dispatcher.UIThread.RunJobs();
    var spinnerWhileRunning = spinner.IsVisible;

    pending.SetResult(new UpdatePreparation.UpToDate());
    Dispatcher.UIThread.RunJobs();

    Assert.Multiple(() =>
                    {
                      Assert.That(spinnerWhileRunning, Is.True);
                      Assert.That(spinner.IsVisible, Is.False);
                    });
    await running;
  }
}
