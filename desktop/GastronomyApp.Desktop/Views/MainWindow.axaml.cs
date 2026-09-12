using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace GastronomyApp.Desktop.Views;

public partial class MainWindow : Window
{
  public MainWindow()
  {
    InitializeComponent();
    Closing += OnClosingMinimisesInstead;
  }

  public void BringToFront()
  {
    Show();
    WindowState = WindowState.Normal;
    Activate();
  }

  private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
  {
    if (!eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
    {
      return;
    }

    if (ClickLandsOnAButtonOrDropdown(eventArgs.Source))
    {
      return;
    }

    BeginMoveDrag(eventArgs);
  }

  private static bool ClickLandsOnAButtonOrDropdown(object? source)
  {
    for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
    {
      if (visual is Button or ComboBox)
      {
        return true;
      }
    }

    return false;
  }

  private void OnClosingMinimisesInstead(object? sender, WindowClosingEventArgs eventArgs)
  {
    if (eventArgs.IsProgrammatic)
    {
      return;
    }

    eventArgs.Cancel = true;
    WindowState = WindowState.Minimized;
  }
}
