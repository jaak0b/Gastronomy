namespace GastronomyApp.Contracts;

public sealed record AdminStationView(Guid StationId, string Name, int SortOrder, bool IsActive, bool HasDevice, DateTime? LastSeenAtUtc, bool HasOutstandingInvitation, bool IsAtTheFestival);
