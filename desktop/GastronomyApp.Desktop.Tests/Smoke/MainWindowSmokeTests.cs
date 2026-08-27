using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using FakeItEasy;
using GastronomyApp.Api.Hosting;
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
            .Returns(new DesktopSettings(5000, "0.0.0.0", @"C:\ProgramData\GastronomyApp", null));

        INetworkAddressProvider networkAddressProvider = A.Fake<INetworkAddressProvider>();
        A.CallTo(() => networkAddressProvider.GetAvailableAddresses())
            .Returns(new List<NetworkAddressOption> { new("WiFi", "192.168.1.20") });

        IQrCodeGenerator qrCodeGenerator = A.Fake<IQrCodeGenerator>();
        A.CallTo(() => qrCodeGenerator.GenerateMatrix(A<string>._))
            .Returns(new List<bool[]> { new[] { true, false }, new[] { false, true } });

        return new MainWindowViewModel(
            A.Fake<IHostLauncher>(),
            A.Fake<IPowerManager>(),
            qrCodeGenerator,
            networkAddressProvider,
            settingsStore,
            text);
    }

    [AvaloniaTest]
    public void MainWindow_Loads_WithTheTitleResolvedFromTheResxTable()
    {
        MainWindowViewModel viewModel = CreateMainWindowViewModel();
        viewModel.RefreshAddress();

        MainWindow window = new() { DataContext = viewModel };
        window.Show();

        Assert.That(window.Title, Is.EqualTo(text.Get("desktop.windowTitle")));
    }

    [AvaloniaTest]
    public async Task SettingsWindow_Loads_WithTheTitleResolvedFromTheResxTable()
    {
        ISettingsStore settingsStore = A.Fake<ISettingsStore>();
        A.CallTo(() => settingsStore.Load())
            .Returns(new DesktopSettings(5000, "0.0.0.0", @"C:\ProgramData\GastronomyApp", null));
        ISessionStateQuery sessionState = A.Fake<ISessionStateQuery>();
        A.CallTo(() => sessionState.IsSessionActiveAsync(A<CancellationToken>._)).Returns(false);

        SettingsWindowViewModel viewModel = new(
            settingsStore,
            A.Fake<INetworkAddressProvider>(),
            sessionState,
            A.Fake<IElevatedSetupLauncher>(),
            text,
            () => { },
            () => { },
            anyOrderAcceptedThisSession: false);
        await viewModel.InitializeAsync();

        SettingsWindow window = new() { DataContext = viewModel };
        window.Show();

        Assert.That(window.Title, Is.EqualTo(text.Get("desktop.settings.title")));
    }
}
