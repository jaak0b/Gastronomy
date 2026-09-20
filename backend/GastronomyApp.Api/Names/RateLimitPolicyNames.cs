namespace GastronomyApp.Api.Names;

public sealed record RateLimitPolicyNames
{
  public string PerDevice { get; } = "per-device";

  public string PerAddress { get; } = "per-address";
}
