namespace GastronomyApp.Contracts;

public sealed record AdminItemView(Guid ItemId, string Name, Guid CategoryId, int SortOrder, bool IsActive, double? ProductionMinutes, bool IsQueueIndependent, AdminItemAtFestivalView? AtTheFestival);
