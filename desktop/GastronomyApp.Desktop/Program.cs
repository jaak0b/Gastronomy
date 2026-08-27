using Avalonia;
using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop;

internal sealed class Program
{
    private const string SetupArgument = "--setup";

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains(SetupArgument))
        {
            new DesktopComposition().RunElevatedSetupSteps();

            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
