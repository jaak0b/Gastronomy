using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Threading;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.Tests.TestSupport;
using GastronomyApp.Desktop.ViewModels;
using GastronomyApp.Desktop.Views;

namespace GastronomyApp.Desktop.Tests.Smoke;

[TestFixture]
public sealed class QuitConfirmDialogSmokeTest
{
  private readonly IDesktopTextProvider _text = new DesktopTextProvider();
  private readonly RenderedColourReader _colours = new();
  private readonly HeadlessButtonClick _clicks = new();

  private QuitConfirmViewModel CreateQuitConfirmViewModel()
  {
    return new(_ => Task.CompletedTask, _text, () => { }, _ => Task.CompletedTask);
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
                      Assert.That(_colours.ReadLabelBackground(confirm), Is.EqualTo(Color.Parse("#C94F4F")));
                      Assert.That(_colours.LabelBackgroundWhile(confirm, ":pointerover"), Is.EqualTo(Color.Parse("#D96060")));
                      Assert.That(_colours.LabelBackgroundWhile(confirm, ":pressed"), Is.EqualTo(Color.Parse("#A83E3E")));
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

  [AvaloniaTest]
  public async Task QuitConfirmDialog_ShowsTheOutcomeOfTheClickedButton()
  {
    Window owner = new();
    owner.Show();
    var viewModel = CreateQuitConfirmViewModel();

    QuitConfirmDialog cancelDialog = new() { DataContext = viewModel };
    Task<bool> cancelResult = cancelDialog.ShowDialog<bool>(owner);
    Dispatcher.UIThread.RunJobs();
    _clicks.Click(cancelDialog, cancelDialog.FindControl<Button>("CancelButton")!);
    Assert.That(await cancelResult, Is.False);

    QuitConfirmDialog confirmDialog = new() { DataContext = viewModel };
    Task<bool> confirmResult = confirmDialog.ShowDialog<bool>(owner);
    Dispatcher.UIThread.RunJobs();
    _clicks.Click(confirmDialog, confirmDialog.FindControl<Button>("ConfirmButton")!);
    Assert.That(await confirmResult, Is.True);
  }
}
