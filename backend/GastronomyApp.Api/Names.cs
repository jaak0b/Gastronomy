namespace GastronomyApp.Api;

public static class Names
{
  public static class AuthenticationSchemes
  {
    public const string Device = "Device";
  }

  public static class DeviceClaims
  {
    public const string DeviceId = "device_id";

    public const string Language = "language";
  }

  public static class HubEvents
  {
    public const string OrderStatusChanged = "OrderStatusChanged";

    public const string OrderItemsSettled = "OrderItemsSettled";

    public const string StationOrdersChanged = "StationOrdersChanged";

    public const string StationsChanged = "StationsChanged";

    public const string FestivalChanged = "FestivalChanged";

    public const string CatalogChanged = "CatalogChanged";

    public const string EnrolmentCompleted = "EnrolmentCompleted";

    public const string DeviceRevoked = "DeviceRevoked";
  }

  public static class HubGroups
  {
    public const string Devices = "devices";

    public const string Stations = "stations";

    public const string Admin = "admin";

    public static string BuildDeviceGroupName(Guid deviceId)
    {
      return $"device:{deviceId}";
    }

    public static string BuildStationGroupName(Guid stationId)
    {
      return $"station:{stationId}";
    }
  }

  public static class RateLimitPolicies
  {
    public const string PerDevice = "per-device";

    public const string PerAddress = "per-address";
  }
}
