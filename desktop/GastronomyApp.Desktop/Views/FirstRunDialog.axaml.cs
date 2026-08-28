using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GastronomyApp.Desktop.Views;

public partial class FirstRunDialog : Window
{
  public FirstRunDialog()
  {
    InitializeComponent();

    var continueButton = this.FindControl<Button>("ContinueButton");
    if (continueButton is not null)
    {
      continueButton.Click += OnContinueClicked;
    }
  }

  private void OnContinueClicked(object? sender, RoutedEventArgs eventArgs)
  {
    Close(true);
  }
}
