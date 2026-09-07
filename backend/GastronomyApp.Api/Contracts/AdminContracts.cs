using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record AdminStationView(
  Guid StationId,
  string Name,
  int SortOrder,
  bool IsActive,
  bool HasDevice,
  DateTime? LastSeenAtUtc,
  bool HasOutstandingInvitation);

public sealed record AdminStationListView(IReadOnlyList<AdminStationView> Stations);

public sealed record SaveStationRequest
{
  public required string? Name { get; init; }

  public required int SortOrder { get; init; }
}

public sealed record SavedStationView(Guid StationId);

public sealed record AdminItemView(
  Guid ItemId,
  string Name,
  string CategoryName,
  int PriceCents,
  int SortOrder,
  bool IsActive,
  bool IsAvailable,
  int? ProductionMinutes,
  IReadOnlyList<Guid> StationIds);

public sealed record AdminItemListView(IReadOnlyList<AdminItemView> Items);

public sealed record SaveItemRequest
{
  public required string? Name { get; init; }

  public required string? CategoryName { get; init; }

  public required int PriceCents { get; init; }

  public required int SortOrder { get; init; }

  public required IReadOnlyList<Guid>? StationIds { get; init; }

  public int? ProductionMinutes { get; init; }
}

public sealed record SetAvailabilityRequest
{
  public required bool IsAvailable { get; init; }
}

public sealed record AdminStaffMemberView(
  Guid StaffMemberId,
  string Name,
  bool IsActive,
  bool HasDevice,
  DateTime? LastSeenAtUtc,
  bool HasOutstandingInvitation);

public sealed record AdminStaffMemberListView(IReadOnlyList<AdminStaffMemberView> StaffMembers);

public sealed record RenameStaffMemberRequest
{
  public required string? Name { get; init; }
}

public sealed record CreateInvitationRequest
{
  public Guid? StaffMemberId { get; init; }

  public Guid? StationId { get; init; }
}

public sealed record InvitationView(
  Guid InvitationId,
  string QrUrl,
  DateTime ExpiresAtUtc,
  DeviceOwnerKind? OwnerKind,
  StaffMemberView? StaffMember,
  StationSummaryView? Station,
  IReadOnlyList<string> AvailableAddresses);

public sealed record ResetNumbersView(int StationCountersCleared);
