using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record AdminStationView(
  Guid StationId,
  string Name,
  int SortOrder,
  bool IsActive,
  bool HasDevice,
  DateTime? LastSeenAtUtc,
  bool HasOutstandingInvitation,
  bool IsAtTheFestival);

public sealed record AdminStationListView(IReadOnlyList<AdminStationView> Stations);

public sealed record SaveStationRequest
{
  public required string? Name { get; init; }

  public required int SortOrder { get; init; }
}

public sealed record SavedStationView(Guid StationId);

public sealed record AdminCategoryView(
  Guid CategoryId,
  string Name,
  string ColourHex,
  int SortOrder,
  bool IsActive);

public sealed record AdminCategoryListView(IReadOnlyList<AdminCategoryView> Categories);

public sealed record SaveCategoryRequest
{
  public required string? Name { get; init; }

  public required string? ColourHex { get; init; }
}

public sealed record MoveCategoryRequest
{
  public required CategoryMoveDirection Direction { get; init; }
}

public sealed record AdminItemAtFestivalView(
  int PriceCents,
  bool IsAvailable,
  IReadOnlyList<Guid> StationIds);

public sealed record AdminItemView(
  Guid ItemId,
  string Name,
  Guid CategoryId,
  int SortOrder,
  bool IsActive,
  int? ProductionMinutes,
  AdminItemAtFestivalView? AtTheFestival);

public sealed record AdminItemListView(IReadOnlyList<AdminItemView> Items);

public sealed record SaveItemRequest
{
  public required string? Name { get; init; }

  public required Guid? CategoryId { get; init; }

  public required int SortOrder { get; init; }

  public int? ProductionMinutes { get; init; }
}

public sealed record SaveFestivalItemRequest
{
  public required int PriceCents { get; init; }

  public required IReadOnlyList<Guid>? StationIds { get; init; }
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

public sealed record AdminFestivalView(
  Guid FestivalId,
  string Name,
  DateTime StartsAtUtc,
  DateTime EndsAtUtc,
  bool IsHidden,
  bool IsRunning,
  int StationCount,
  int MenuItemCount,
  int OrderCount);

public sealed record AdminFestivalListView(IReadOnlyList<AdminFestivalView> Festivals);

public sealed record SaveFestivalRequest
{
  public required string? Name { get; init; }

  public required DateTime StartsAtUtc { get; init; }

  public required DateTime EndsAtUtc { get; init; }
}

public sealed record SavedFestivalView(Guid FestivalId);
