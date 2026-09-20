using Avalonia;
using Avalonia.Headless;
using GastronomyApp.Desktop.Tests.TestSupport;

[assembly: AvaloniaTestApplication(typeof(HeadlessAppBuilder))]

namespace GastronomyApp.Desktop.Tests.TestSupport;

public sealed class HeadlessAppBuilder
{
  public static AppBuilder BuildAvaloniaApp()
  {
    return AppBuilder.Configure<App>().UseHeadless(new());
  }
}
