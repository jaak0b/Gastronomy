using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Threading;
using FakeItEasy;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Setup;
using GastronomyApp.Desktop.ViewModels;
using GastronomyApp.Desktop.Views;

namespace GastronomyApp.Desktop.Tests.Smoke;

[TestFixture]
public sealed class FirstRunDialogSmokeTest
{
  private readonly IDesktopTextProvider _text = new DesktopTextProvider();
  private readonly HeadlessButtonClick _clicks = new();

  private FirstRunViewModel CreateFirstRunViewModel()
  {
    return new(A.Fake<IFirewallSetup>(), A.Fake<IDataFolderSetup>(), A.Fake<IElevatedSetupLauncher>(), _text);
  }

  [AvaloniaTest]
  public void FirstRunDialog_DrawsItsTextWithoutTheColouredFringesOfSubpixelSmoothing()
  {
    FirstRunDialog dialog = new() { DataContext = CreateFirstRunViewModel() };
    dialog.Show();
    Dispatcher.UIThread.RunJobs();

    Assert.That(TextOptions.GetTextRenderingMode(dialog), Is.EqualTo(TextRenderingMode.Antialias));
  }

  [AvaloniaTest]
  public async Task FirstRunDialog_WhenTheContinueButtonIsClicked_ClosesWithTrue()
  {
    Window owner = new();
    owner.Show();
    FirstRunDialog dialog = new() { DataContext = CreateFirstRunViewModel() };

    Task<bool> result = dialog.ShowDialog<bool>(owner);
    Dispatcher.UIThread.RunJobs();

    _clicks.Click(dialog, dialog.FindControl<Button>("ContinueButton")!);

    Assert.That(await result, Is.True);
  }
}
