namespace GastronomyApp.Desktop.Events;

public sealed class UpdateReadyRequestedEventArgs(string version) : EventArgs
{
  public string Version { get; } = version;
}
