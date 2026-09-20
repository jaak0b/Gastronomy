namespace GastronomyApp.Api.RateLimiting;

public sealed record RateLimitPolicyNames
{
  public string PerDevice { get; } = "per-device";

  public string PerAddress { get; } = "per-address";
}
