using Avalonia;
using GastronomyApp.Desktop.Services;
using Velopack;

namespace GastronomyApp.Desktop;

sealed internal class Program
{
  private const string SetupArgument = "--setup";

  [STAThread]
  public static void Main(string[] args)
  {
    VelopackApp.Build().Run();

    ApplicationLog log = new();
    DesktopComposition composition = new();

    try
    {
      if (args.Contains(SetupArgument))
      {
        Environment.ExitCode = new ElevatedSetupEntryPoint(log,
                                                           composition.DataDirectoryPath,
                                                           composition.ElevatedSetupSteps).Run();

        return;
      }

      log.Start(composition.DataDirectoryPath);
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
