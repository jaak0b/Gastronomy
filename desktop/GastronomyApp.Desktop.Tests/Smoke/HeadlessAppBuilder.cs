using Avalonia;
using Avalonia.Headless;
using GastronomyApp.Desktop.Tests.Smoke;

[assembly: AvaloniaTestApplication(typeof(HeadlessAppBuilder))]

namespace GastronomyApp.Desktop.Tests.Smoke;

public sealed class HeadlessAppBuilder
{
  public static AppBuilder BuildAvaloniaApp()
  {
    return AppBuilder.Configure<App>().UseHeadless(new());
  }
}
