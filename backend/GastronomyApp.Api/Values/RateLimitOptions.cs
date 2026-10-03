namespace GastronomyApp.Api.Values;

public sealed record RateLimitOptions
{
  public int DeviceRequestsPerMinute { get; init; } = 600;

  public int AddressRequestsPerMinute { get; init; } = 20;
}
