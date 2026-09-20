namespace GastronomyApp.Desktop.Values;

public sealed record StoredSettings
{
  public int? Port { get; init; }

  public string? DataDirectory { get; init; }

  public string? SelectedNetworkInterface { get; init; }

  public string? Language { get; init; }

  public DateTimeOffset? LastUpdateCheckUtc { get; init; }
}
