using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GastronomyApp.Desktop.Views;

public partial class QuitConfirmDialog : Window
{
  public QuitConfirmDialog()
  {
    InitializeComponent();

    Button? cancelButton = this.FindControl<Button>("CancelButton");
    if (cancelButton is not null)
    {
      cancelButton.Click += OnCancelClicked;
    }

    Button? confirmButton = this.FindControl<Button>("ConfirmButton");
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
