namespace GastronomyApp.Api.Contracts;

public sealed record AdminStationView(
    Guid StationId,
    string Name,
    int SortOrder,
    bool IsActive,
    string TransportKind,
    string? Host,
    int Port,
    bool IsEnabled,
    bool IsOnline,
    bool IsPaperEnd,
    bool IsCoverOpen,
    bool IsFaulty);

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
    IReadOnlyList<Guid> StationIds);

public sealed record AdminItemListView(IReadOnlyList<AdminItemView> Items);

public sealed record SaveItemRequest
{
    public required string? Name { get; init; }
    public required string? CategoryName { get; init; }
    public required int PriceCents { get; init; }
    public required int SortOrder { get; init; }
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
    string? UserAgent,
    bool HasOutstandingInvitation);

public sealed record AdminStaffMemberListView(IReadOnlyList<AdminStaffMemberView> StaffMembers);

public sealed record RenameStaffMemberRequest
{
    public required string? Name { get; init; }
}

public sealed record CreateInvitationRequest
{
    public Guid? StaffMemberId { get; init; }
}

public sealed record InvitationView(
    Guid InvitationId,
    string QrUrl,
    string SixDigitCode,
    DateTime ExpiresAtUtc,
    StaffMemberView? StaffMember,
    IReadOnlyList<string> AvailableAddresses);

public sealed record AdminPrinterView(
    Guid StationId,
    string StationName,
    string TransportKind,
    string? Host,
    int Port,
    string? AgentIdentifier,
    int CharactersPerLine,
    string CodePageName,
    int ConnectTimeoutSeconds,
    int JobTimeoutSeconds,
    int HeartbeatSeconds,
    bool IsEnabled,
    bool IsOnline,
    bool IsPaperEnd,
    bool IsPaperNearEnd,
    bool IsCoverOpen,
    bool IsFaulty,
    int WaitingTicketCount,
    DateTime? LastChangedAtUtc,
    IReadOnlyList<string> SharedWithStationNames,
    string? MockFolderPath);

public sealed record AdminPrinterListView(IReadOnlyList<AdminPrinterView> Printers);

public sealed record SavePrinterRequest
{
    public required string? TransportKind { get; init; }
    public string? Host { get; init; }
    public required int Port { get; init; }
    public string? AgentIdentifier { get; init; }
    public required int CharactersPerLine { get; init; }
    public required string? CodePageName { get; init; }
    public required int ConnectTimeoutSeconds { get; init; }
    public required int JobTimeoutSeconds { get; init; }
    public required int HeartbeatSeconds { get; init; }
    public required bool IsEnabled { get; init; }
}

public sealed record SharedEndpointView(Guid StationId, IReadOnlyList<Guid> StationsSharingThisEndpoint);

public sealed record ReconnectedView(Guid StationId, IReadOnlyList<Guid> ClearedStationIds);

public sealed record ArmMockFaultRequest
{
    public required string? Fault { get; init; }
    public required string? Mode { get; init; }
}

public sealed record ResetNumbersView(int StationCountersCleared);
