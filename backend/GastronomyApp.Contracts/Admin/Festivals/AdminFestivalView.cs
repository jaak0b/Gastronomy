namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record AdminFestivalView(Guid FestivalId, string Name, DateTime StartsAtUtc, DateTime EndsAtUtc, bool IsHidden, bool IsRunning, int StationCount, int MenuItemCount, int OrderCount);
