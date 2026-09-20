using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;

namespace GastronomyApp.Desktop.Tests.Smoke;

sealed internal class HeadlessButtonClick
{
  public void Click(Window window, Button button)
  {
    var center = button.TranslatePoint(new(button.Bounds.Width / 2, button.Bounds.Height / 2), window) ?? new Point();

    window.MouseDown(center, MouseButton.Left);
    window.MouseUp(center, MouseButton.Left);
    Dispatcher.UIThread.RunJobs();
  }
}
