using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using GastronomyApp.Desktop.Localization;
using GastronomyApp.Desktop.ViewModels;
using GastronomyApp.Desktop.Views;

namespace GastronomyApp.Desktop.Tests.Smoke;

[TestFixture]
public sealed class UpdateConfirmDialogSmokeTest
{
  private const string Version = "9.9.9";

  private readonly IDesktopTextProvider _text = new DesktopTextProvider();
  private readonly HeadlessButtonClick _clicks = new();

  [AvaloniaTest]
  public async Task UpdateConfirmDialog_ShowsTheVersionAndReturnsThePressedChoice()
  {
    UpdateConfirmViewModel viewModel = new(Version, _text);

    Window owner = new();
    owner.Show();

    UpdateConfirmDialog cancelDialog = new() { DataContext = viewModel };
    Task<bool> cancelResult = cancelDialog.ShowDialog<bool>(owner);
    Dispatcher.UIThread.RunJobs();

    Assert.Multiple(() =>
                    {
                      Assert.That(cancelDialog.Title, Is.EqualTo(_text.Get("desktop.update.confirmTitle")));
                      Assert.That(viewModel.Title, Is.EqualTo(_text.Get("desktop.update.confirmTitle")));
                      Assert.That(viewModel.Body, Is.EqualTo(_text.Format("desktop.update.confirmBody", new TextPlaceholder("version", Version))));
                      Assert.That(viewModel.Body, Does.Contain(Version));
                    });

    _clicks.Click(cancelDialog, cancelDialog.FindControl<Button>("CancelButton")!);
    Assert.That(await cancelResult, Is.False);

    UpdateConfirmDialog confirmDialog = new() { DataContext = viewModel };
    Task<bool> confirmResult = confirmDialog.ShowDialog<bool>(owner);
    Dispatcher.UIThread.RunJobs();
    _clicks.Click(confirmDialog, confirmDialog.FindControl<Button>("ConfirmButton")!);
    Assert.That(await confirmResult, Is.True);
  }
}
