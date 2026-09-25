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
    public const string ConfigurationChanged = "ConfigurationChanged";

    public const string OrdersChanged = "OrdersChanged";

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
  }

  public static class RateLimitPolicies
  {
    public const string PerDevice = "per-device";

    public const string PerAddress = "per-address";
  }
}
