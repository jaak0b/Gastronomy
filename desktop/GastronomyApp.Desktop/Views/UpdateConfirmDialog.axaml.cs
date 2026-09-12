using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GastronomyApp.Desktop.Views;

public partial class UpdateConfirmDialog : Window
{
  public UpdateConfirmDialog()
  {
    InitializeComponent();

    var cancelButton = this.FindControl<Button>("CancelButton");
    if (cancelButton is not null)
    {
      cancelButton.Click += OnCancelClicked;
    }

    var confirmButton = this.FindControl<Button>("ConfirmButton");
    if (confirmButton is not null)
    {
      confirmButton.Click += OnConfirmClicked;
    }
  }

  private void OnCancelClicked(object? sender, RoutedEventArgs eventArgs)
  {
    Close(false);
  }

  private void OnConfirmClicked(object? sender, RoutedEventArgs eventArgs)
  {
    Close(true);
  }
}
