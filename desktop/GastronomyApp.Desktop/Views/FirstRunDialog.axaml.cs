using Avalonia.Controls;
using GastronomyApp.Desktop.ViewModels;

namespace GastronomyApp.Desktop.Views;

public partial class FirstRunDialog : Window
{
  public FirstRunDialog()
  {
    InitializeComponent();

    DataContextChanged += OnDataContextChanged;
  }

  private void OnDataContextChanged(object? sender, EventArgs e)
  {
    if (DataContext is FirstRunViewModel viewModel)
    {
      viewModel.CloseRequested += OnCloseRequested;
    }
  }

  private void OnCloseRequested(object? sender, EventArgs e)
  {
    if (DataContext is FirstRunViewModel viewModel)
    {
      viewModel.CloseRequested -= OnCloseRequested;
    }

    Close(true);
  }
}
