using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GastronomyApp.Desktop.Views;

public partial class FailureDetailDialog : Window
{
  public FailureDetailDialog()
  {
    InitializeComponent();

    var closeButton = this.FindControl<Button>("CloseButton");
    if (closeButton is not null)
    {
      closeButton.Click += OnCloseClicked;
    }
  }

  private void OnCloseClicked(object? sender, RoutedEventArgs eventArgs)
  {
    Close();
  }
}
