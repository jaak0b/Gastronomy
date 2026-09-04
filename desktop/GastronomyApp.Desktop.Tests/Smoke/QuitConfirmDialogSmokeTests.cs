using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Threading;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Services;
using GastronomyApp.Desktop.ViewModels;
using GastronomyApp.Desktop.Views;

namespace GastronomyApp.Desktop.Tests.Smoke;

[TestFixture]
public sealed class QuitConfirmDialogSmokeTests
{
  private readonly IDesktopTextProvider _text = new DesktopTextProvider();

  private QuitConfirmViewModel CreateQuitConfirmViewModel()
  {
    return new(_ => Task.CompletedTask, _text, () => { });
  }

  [AvaloniaTest]
  public void QuitConfirmDialog_ShowsTheConfirmButtonRespondingToBeingHoveredAndPressed()
  {
    QuitConfirmDialog dialog = new() { DataContext = CreateQuitConfirmViewModel() };
    dialog.Show();
    Dispatcher.UIThread.RunJobs();

    var confirm = dialog.FindControl<Button>("ConfirmButton")!;

    Assert.Multiple(() =>
                    {
                      Assert.That(RenderedColours.LabelBackgroundOf(confirm),
                                  Is.EqualTo(Color.Parse("#DC2626")));
                      Assert.That(RenderedColours.LabelBackgroundWhile(confirm, ":pointerover"),
                                  Is.EqualTo(Color.Parse("#EF4444")));
                      Assert.That(RenderedColours.LabelBackgroundWhile(confirm, ":pressed"),
                                  Is.EqualTo(Color.Parse("#B91C1C")));
                    });
  }

  [AvaloniaTest]
  public void QuitConfirmDialog_DrawsItsTextWithoutTheColouredFringesOfSubpixelSmoothing()
  {
    QuitConfirmDialog dialog = new() { DataContext = CreateQuitConfirmViewModel() };
    dialog.Show();
    Dispatcher.UIThread.RunJobs();

    Assert.That(TextOptions.GetTextRenderingMode(dialog), Is.EqualTo(TextRenderingMode.Antialias));
  }
}
