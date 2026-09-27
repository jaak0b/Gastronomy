using Avalonia.Controls;
using Avalonia.Input;

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

  private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
  {
    WindowDrag.BeginIfTitleBarPressed(this, e);
  }

  private void OnClosingMinimisesInstead(object? sender, WindowClosingEventArgs e)
  {
    if (e.IsProgrammatic)
      return;

    e.Cancel = true;
    WindowState = WindowState.Minimized;
  }
}
