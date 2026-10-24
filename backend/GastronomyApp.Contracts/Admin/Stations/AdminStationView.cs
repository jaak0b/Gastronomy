namespace GastronomyApp.Contracts.Admin.Stations;

public sealed record AdminStationView(Guid StationId, string Name, int SortOrder, bool IsActive, bool HasDevice, bool IsAtAnyFestival, bool HasItemsToHandOutAtTheRunningFestival, int? StrandedItemCountWhenSwitchedOff, int? StrandedItemCountWhenRemovedFromTheFestival);
