namespace GastronomyApp.Core.ReadModels;

public sealed record AdministeredFestival(
  Guid FestivalId,
  string Name,
  DateTime StartsAtUtc,
  DateTime EndsAtUtc,
  bool IsHidden,
  bool IsRunning,
  int StationCount,
  int MenuItemCount,
  int OrderCount);
