using System.Text.Json.Serialization;
namespace GastronomyApp.Api.Contracts;

public sealed record AdminStationView(
    Guid StationId,
    string Name,
    int SortOrder,
    bool IsActive,
    Guid? PrinterId,
    string? PrinterName,
    bool IsOnline,
    bool IsPaperEnd,
    bool IsCoverOpen,
    bool IsFaulty);

public sealed record AdminStationListView(IReadOnlyList<AdminStationView> Stations);

public sealed record SaveStationRequest
{
    public required string? Name { get; init; }
    public required int SortOrder { get; init; }
    public Guid? PrinterId { get; init; }
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
    DateTime ExpiresAtUtc,
    StaffMemberView? StaffMember,
    IReadOnlyList<string> AvailableAddresses);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "printerType")]
[JsonDerivedType(typeof(TestPrinterView), "TestPrinter")]
[JsonDerivedType(typeof(EpsonTmT20ivNetworkPrinterView), "EpsonTmT20ivNetworkPrinter")]
public abstract record AdminPrinterView
{
    public required Guid PrinterId { get; init; }
    public required string Name { get; init; }
    public required bool IsOnline { get; init; }
    public required bool IsPaperEnd { get; init; }
    public required bool IsPaperNearEnd { get; init; }
    public required bool IsCoverOpen { get; init; }
    public required bool IsFaulty { get; init; }
    public required int WaitingTicketCount { get; init; }
    public required DateTime? LastChangedAtUtc { get; init; }
    public required string? StatusDetail { get; init; }
    public required IReadOnlyList<string> StationNames { get; init; }
}

public sealed record TestPrinterView : AdminPrinterView
{
    public required string SimulatedFault { get; init; }
    public required string SimulatedFaultMode { get; init; }
}

public sealed record EpsonTmT20ivNetworkPrinterView : AdminPrinterView
{
    public required string Host { get; init; }
    public required int Port { get; init; }
}

public sealed record AdminPrinterListView(IReadOnlyList<AdminPrinterView> Printers);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "printerType")]
[JsonDerivedType(typeof(SaveTestPrinterRequest), "TestPrinter")]
[JsonDerivedType(typeof(SaveEpsonTmT20ivNetworkPrinterRequest), "EpsonTmT20ivNetworkPrinter")]
public abstract record SavePrinterRequest
{
    public required string? Name { get; init; }
}

public sealed record SaveTestPrinterRequest : SavePrinterRequest
{
    public string? SimulatedFault { get; init; }
    public string? SimulatedFaultMode { get; init; }
}

public sealed record SaveEpsonTmT20ivNetworkPrinterRequest : SavePrinterRequest
{
    public required string? Host { get; init; }
    public required int Port { get; init; }
}

public sealed record SavedPrinterView(Guid PrinterId, IReadOnlyList<string> StationNames);

public sealed record ReconnectedView(Guid PrinterId, IReadOnlyList<Guid> ClearedStationIds);

public sealed record ResetNumbersView(int StationCountersCleared);
