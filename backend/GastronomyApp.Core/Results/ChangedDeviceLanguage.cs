namespace GastronomyApp.Core.Results;

public sealed record ChangedDeviceLanguage
{
  public required Guid DeviceId { get; init; }

  public required string Language { get; init; }
}
