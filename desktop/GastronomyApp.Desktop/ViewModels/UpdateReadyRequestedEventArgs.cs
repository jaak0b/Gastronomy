namespace GastronomyApp.Desktop.ViewModels;

public sealed class UpdateReadyRequestedEventArgs(string version) : EventArgs
{
  public string Version { get; } = version;
}
