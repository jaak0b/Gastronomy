namespace GastronomyApp.Core.ReadModels;

public sealed record FestivalContentCounts(Guid FestivalId, int StationCount, int MenuItemCount, int OrderCount);
