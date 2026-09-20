using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Threading;
using FakeItEasy;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Services;
using GastronomyApp.Desktop.ViewModels;
using GastronomyApp.Desktop.Views;

namespace GastronomyApp.Desktop.Tests.Smoke;

[TestFixture]
public sealed class FirstRunDialogSmokeTests
{
  private readonly IDesktopTextProvider _text = new DesktopTextProvider();

  private FirstRunViewModel CreateFirstRunViewModel()
  {
    return new(A.Fake<IFirewallSetup>(),
               A.Fake<IDataFolderSetup>(),
               A.Fake<IElevatedSetupLauncher>(),
               _text);
  }

  [AvaloniaTest]
  public void FirstRunDialog_DrawsItsTextWithoutTheColouredFringesOfSubpixelSmoothing()
  {
    FirstRunDialog dialog = new() { DataContext = CreateFirstRunViewModel() };
    dialog.Show();
    Dispatcher.UIThread.RunJobs();

    Assert.That(TextOptions.GetTextRenderingMode(dialog), Is.EqualTo(TextRenderingMode.Antialias));
  }
}
