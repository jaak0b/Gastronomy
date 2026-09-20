namespace GastronomyApp.Desktop.Services;

public sealed record DesktopSettings(
  int? Port,
  string DataDirectory,
  string? SelectedNetworkInterface,
  string? Language,
  DateTimeOffset? LastUpdateCheckUtc);
