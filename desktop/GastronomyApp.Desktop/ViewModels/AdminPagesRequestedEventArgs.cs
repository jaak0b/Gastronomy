namespace GastronomyApp.Desktop.ViewModels;

public sealed class AdminPagesRequestedEventArgs(string url) : EventArgs
{
  public string Url { get; } = url;
}
