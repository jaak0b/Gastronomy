using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record OrderAcceptanceResult
{
  public required Order Order { get; init; }

  public required bool WasAlreadyAccepted { get; init; }
}
