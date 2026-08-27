using Avalonia.Controls;

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
