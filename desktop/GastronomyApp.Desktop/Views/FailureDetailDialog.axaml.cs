using Avalonia.Controls;
using GastronomyApp.Desktop.ViewModels;

namespace GastronomyApp.Desktop.Views;

public partial class FailureDetailDialog : Window
{
  public FailureDetailDialog()
  {
    InitializeComponent();

    DataContextChanged += OnDataContextChanged;
  }

  private void OnDataContextChanged(object? sender, EventArgs e)
  {
    if (DataContext is TechnicalDetailViewModel viewModel)
      viewModel.CloseRequested += OnCloseRequested;
  }

  private void OnCloseRequested(object? sender, EventArgs e)
  {
    if (DataContext is TechnicalDetailViewModel viewModel)
      viewModel.CloseRequested -= OnCloseRequested;

    Close();
  }
}
