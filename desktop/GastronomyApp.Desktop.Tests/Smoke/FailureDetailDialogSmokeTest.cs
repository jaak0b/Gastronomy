using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using GastronomyApp.Desktop.ViewModels;
using GastronomyApp.Desktop.Views;

namespace GastronomyApp.Desktop.Tests.Smoke;

[TestFixture]
public sealed class FailureDetailDialogSmokeTest
{
  [AvaloniaTest]
  public async Task FailureDetailDialog_WhenEscapeIsPressed_Closes()
  {
    Window owner = new();
    owner.Show();
    FailureDetailDialog dialog = new() { DataContext = new TechnicalDetailViewModel("Title", "Detail", "Close") };

    Task closed = dialog.ShowDialog(owner);
    Dispatcher.UIThread.RunJobs();
    dialog.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
    Dispatcher.UIThread.RunJobs();

    await closed;
    Assert.That(dialog.IsVisible, Is.False);
  }
}
