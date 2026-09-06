using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record StaffMemberView(Guid Id, string Name);

public sealed record StationSummaryView(Guid Id, string Name);

public sealed record SessionView(
  Guid DeviceId,
  DeviceOwnerKind DeviceKind,
  StaffMemberView? StaffMember,
  StationSummaryView? Station,
  string Language);

public sealed record LanguageChangeRequest(string? Language);

public sealed record LanguageView(string Language);
