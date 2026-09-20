namespace GastronomyApp.Desktop.Events;

public sealed class AdminPagesRequestedEventArgs(string url) : EventArgs
{
  public string Url { get; } = url;
}
