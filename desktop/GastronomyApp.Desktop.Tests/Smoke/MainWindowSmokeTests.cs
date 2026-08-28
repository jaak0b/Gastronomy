using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using FakeItEasy;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Services;
using GastronomyApp.Desktop.Tests.Smoke;
using GastronomyApp.Desktop.ViewModels;
using GastronomyApp.Desktop.Views;

[assembly: AvaloniaTestApplication(typeof(HeadlessAppBuilder))]

namespace GastronomyApp.Desktop.Tests.Smoke;

public sealed class HeadlessAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}

[TestFixture]
public sealed class MainWindowSmokeTests
{
    private readonly IDesktopTextProvider text = new DesktopTextProvider();

    private MainWindowViewModel CreateMainWindowViewModel()
    {
        ISettingsStore settingsStore = A.Fake<ISettingsStore>();
        A.CallTo(() => settingsStore.Load())
            .Returns(new DesktopSettings(5000, @"C:\ProgramData\GastronomyApp", null, null));

        return new MainWindowViewModel(
            A.Fake<IHostLauncher>(),
            A.Fake<IPowerManager>(),
            settingsStore,
            text,
            A.Fake<IFreePortProvider>());
    }

    [AvaloniaTest]
    public void MainWindow_Loads_WithTheTitleResolvedFromTheResxTable()
    {
        MainWindowViewModel viewModel = CreateMainWindowViewModel();

        MainWindow window = new() { DataContext = viewModel };
        window.Show();

        Assert.That(window.Title, Is.EqualTo(text.Get("desktop.windowTitle")));
    }

}
