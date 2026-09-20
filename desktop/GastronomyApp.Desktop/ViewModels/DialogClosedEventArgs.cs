namespace GastronomyApp.Desktop.ViewModels;

public sealed class DialogClosedEventArgs(bool confirmed) : EventArgs
{
  public bool Confirmed { get; } = confirmed;
}
