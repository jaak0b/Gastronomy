using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace GastronomyApp.Desktop.Views;

public static class WindowDrag
{
  public static void BeginIfTitleBarPressed(Window window, PointerPressedEventArgs e)
  {
    if (!e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)
      return;

    for (var visual = e.Source as Visual; visual is not null; visual = visual.GetVisualParent())
    {
      if (visual is Button or ComboBox)
        return;
    }

    window.BeginMoveDrag(e);
  }
}
