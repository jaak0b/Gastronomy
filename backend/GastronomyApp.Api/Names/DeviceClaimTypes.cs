namespace GastronomyApp.Api.Names;

public sealed record DeviceClaimTypes
{
  public string DeviceId { get; } = "device_id";

  public string OwnerKind { get; } = "device_owner_kind";

  public string Language { get; } = "language";
}
