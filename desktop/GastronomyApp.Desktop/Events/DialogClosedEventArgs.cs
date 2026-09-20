namespace GastronomyApp.Desktop.Events;

public sealed class DialogClosedEventArgs(bool confirmed) : EventArgs
{
  public bool Confirmed { get; } = confirmed;
}
