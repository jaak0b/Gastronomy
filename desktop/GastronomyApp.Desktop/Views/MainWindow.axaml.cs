using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using GastronomyApp.Desktop.ViewModels;

namespace GastronomyApp.Desktop.Views;

public partial class MainWindow : Window
{
    private const double QrCanvasSize = 220;

    private MainWindowViewModel? boundViewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
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

    private void OnDataContextChanged(object? sender, EventArgs eventArgs)
    {
        if (boundViewModel is not null)
        {
            boundViewModel.QrMatrix.CollectionChanged -= OnQrMatrixChanged;
        }

        boundViewModel = DataContext as MainWindowViewModel;

        if (boundViewModel is not null)
        {
            boundViewModel.QrMatrix.CollectionChanged += OnQrMatrixChanged;
        }

        DrawQrCode();
    }

    private void OnQrMatrixChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        DrawQrCode();
    }

    private void DrawQrCode()
    {
        Canvas? canvas = this.FindControl<Canvas>("QrCanvas");
        if (canvas is null)
        {
            return;
        }

        canvas.Children.Clear();

        if (boundViewModel is null || boundViewModel.QrMatrix.Count == 0)
        {
            return;
        }

        int rows = boundViewModel.QrMatrix.Count;
        double cell = QrCanvasSize / rows;
        SolidColorBrush ink = new(Colors.Black);

        for (int rowIndex = 0; rowIndex < rows; rowIndex++)
        {
            bool[] row = boundViewModel.QrMatrix[rowIndex];

            for (int columnIndex = 0; columnIndex < row.Length; columnIndex++)
            {
                if (!row[columnIndex])
                {
                    continue;
                }

                Rectangle module = new()
                {
                    Width = cell,
                    Height = cell,
                    Fill = ink,
                };

                Canvas.SetLeft(module, columnIndex * cell);
                Canvas.SetTop(module, rowIndex * cell);
                canvas.Children.Add(module);
            }
        }
    }
}
