namespace GastronomyApp.Api.Names;

public sealed record HubGroupNames
{
  public string Devices { get; } = "devices";

  public string Stations { get; } = "stations";

  public string Admin { get; } = "admin";

  public string BuildDeviceGroupName(Guid deviceId)
  {
    return $"device:{deviceId}";
  }

  public string BuildStationGroupName(Guid stationId)
  {
    return $"station:{stationId}";
  }
}
