using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace GastronomyApp.Desktop.Views;

public partial class DialogTitleBar : UserControl
{
  public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<DialogTitleBar, string?>(nameof(Text));

  public DialogTitleBar()
  {
    InitializeComponent();
  }

  public string? Text
  {
    get => GetValue(TextProperty);
    set => SetValue(TextProperty, value);
  }

  private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
  {
    if (TopLevel.GetTopLevel(this) is Window window)
      WindowDrag.BeginIfTitleBarPressed(window, e);
  }
}
