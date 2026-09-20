using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(GastronomyApp.Desktop.Tests.Smoke.HeadlessAppBuilder))]

namespace GastronomyApp.Desktop.Tests.Smoke;

public sealed class HeadlessAppBuilder
{
  public static AppBuilder BuildAvaloniaApp()
  {
    return AppBuilder.Configure<App>().UseHeadless(new());
  }
}
