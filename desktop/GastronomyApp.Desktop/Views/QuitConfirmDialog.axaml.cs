using Avalonia.Controls;
using GastronomyApp.Desktop.ViewModels;

namespace GastronomyApp.Desktop.Views;

public partial class QuitConfirmDialog : Window
{
  public QuitConfirmDialog()
  {
    InitializeComponent();

    DataContextChanged += OnDataContextChanged;
  }

  private void OnDataContextChanged(object? sender, EventArgs e)
  {
    if (DataContext is QuitConfirmViewModel viewModel)
      viewModel.CloseRequested += OnCloseRequested;
  }

  private void OnCloseRequested(object? sender, DialogClosedEventArgs e)
  {
    if (DataContext is QuitConfirmViewModel viewModel)
      viewModel.CloseRequested -= OnCloseRequested;

    Close(e.Confirmed);
  }
}
