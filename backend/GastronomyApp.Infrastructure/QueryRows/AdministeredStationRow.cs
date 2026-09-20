using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.QueryRows;

public sealed record AdministeredStationRow
{
  public required Station Station { get; init; }

  public required DateTime? LastSeenAtUtc { get; init; }

  public required bool HasOutstandingInvitation { get; init; }

  public required bool IsAtTheFestival { get; init; }
}
