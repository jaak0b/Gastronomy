namespace GastronomyApp.Api.Contracts;

public sealed record AdminLocationView(
    Guid LocationId,
    string Name,
    int SortOrder,
    string SlipLanguage,
    bool IsActive,
    string AccessKey,
    string BreakGlassUrl,
    string TransportKind,
    string? Host,
    int Port,
    bool IsEnabled,
    bool IsOnline,
    bool IsPaperEnd,
    bool IsCoverOpen,
    bool IsFaulty);

public sealed record AdminLocationListView(IReadOnlyList<AdminLocationView> Locations);

public sealed record SaveLocationRequest
{
    public required string? Name { get; init; }
    public required int SortOrder { get; init; }
    public string? SlipLanguage { get; init; }
}

public sealed record AccessKeyView(Guid LocationId, string AccessKey, string BreakGlassUrl);

public sealed record StationCardView(Guid LocationId, string StationName, string BreakGlassUrl);

public sealed record AdminItemView(
    Guid ItemId,
    string Name,
    string CategoryName,
    int PriceCents,
    int SortOrder,
    bool IsActive,
    bool IsAvailable,
    IReadOnlyList<Guid> LocationIds);

public sealed record AdminItemListView(IReadOnlyList<AdminItemView> Items);

public sealed record SaveItemRequest
{
    public required string? Name { get; init; }
    public required string? CategoryName { get; init; }
    public required int PriceCents { get; init; }
    public required int SortOrder { get; init; }
    public required IReadOnlyList<Guid>? LocationIds { get; init; }
}

public sealed record SetAvailabilityRequest
{
    public required bool IsAvailable { get; init; }
}

public sealed record AdminServerPersonView(
    Guid ServerPersonId,
    string Name,
    bool IsActive,
    bool HasDevice,
    DateTime? LastSeenAtUtc,
    string? UserAgent,
    bool HasOutstandingInvitation);

public sealed record AdminServerPersonListView(IReadOnlyList<AdminServerPersonView> People);

public sealed record RenameServerPersonRequest
{
    public required string? Name { get; init; }
}

public sealed record CreateInvitationRequest
{
    public Guid? ServerPersonId { get; init; }
}

public sealed record InvitationView(
    Guid InvitationId,
    string QrUrl,
    string SixDigitCode,
    DateTime ExpiresAtUtc,
    ServerPersonView? ServerPerson);

public sealed record AdminPrinterView(
    Guid LocationId,
    string LocationName,
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
    bool IsCoverOpen,
    bool IsFaulty);

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

public sealed record SharedEndpointView(Guid LocationId, IReadOnlyList<Guid> LocationsSharingThisEndpoint);

public sealed record ReconnectedView(Guid LocationId, IReadOnlyList<Guid> ClearedLocationIds);

public sealed record ArmMockFaultRequest
{
    public required string? Fault { get; init; }
    public required string? Mode { get; init; }
}

public sealed record StartEventSessionRequest
{
    public required string? Name { get; init; }
    public required bool IsPractice { get; init; }
    public string? ConfirmedName { get; init; }
}

public sealed record EventSessionBlockingConditionView(
    string Guard,
    string MessageKey,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record EventSessionStartRefusedView(
    string Code,
    string MessageKey,
    IReadOnlyDictionary<string, string> Parameters,
    string? Details,
    IReadOnlyList<EventSessionBlockingConditionView> BlockingConditions);

public sealed record EventSessionBlocksView(
    IReadOnlyList<string> ViolatedGuards,
    int NonFinalTicketCount,
    int UnansweredUnknownCount,
    IReadOnlyList<CatalogLocationView> LocationsOnTestPrinter,
    IReadOnlyList<EventSessionBlockingConditionView> BlockingConditions);

public sealed record AdminEventSessionView(
    EventSessionView? EventSession,
    EventSessionBlocksView? BlocksStarting);

public sealed record StartedEventSessionView(EventSessionView EventSession, DateTime StartedAtUtc);
