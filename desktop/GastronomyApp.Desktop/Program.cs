using Avalonia;
using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop;

sealed internal class Program
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

    ApplicationLog log = new();

    try
    {
      log.Start(new DesktopComposition().DataDirectoryPath);
      BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    } finally
    {
      log.Stop();
    }
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
