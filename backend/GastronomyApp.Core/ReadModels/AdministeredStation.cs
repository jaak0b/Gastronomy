namespace GastronomyApp.Core.ReadModels;

public sealed record AdministeredStation(Guid StationId, string Name, int SortOrder, bool IsActive, bool HasDevice, DateTime? LastSeenAtUtc, bool HasOutstandingInvitation, bool IsAtTheFestival);
