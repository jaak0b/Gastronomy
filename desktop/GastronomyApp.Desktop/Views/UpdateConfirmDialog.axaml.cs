using Avalonia.Controls;
using GastronomyApp.Desktop.ViewModels;

namespace GastronomyApp.Desktop.Views;

public partial class UpdateConfirmDialog : Window
{
  public UpdateConfirmDialog()
  {
    InitializeComponent();

    DataContextChanged += OnDataContextChanged;
  }

  private void OnDataContextChanged(object? sender, EventArgs e)
  {
    if (DataContext is UpdateConfirmViewModel viewModel)
    {
      viewModel.CloseRequested += OnCloseRequested;
    }
  }

  private void OnCloseRequested(object? sender, DialogClosedEventArgs e)
  {
    if (DataContext is UpdateConfirmViewModel viewModel)
    {
      viewModel.CloseRequested -= OnCloseRequested;
    }

    Close(e.Confirmed);
  }
}
