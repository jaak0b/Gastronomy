using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record EventSessionStartDecision
{
    public required EventSession SessionToStart { get; init; }
    public required EventSession? PreviousSessionToEnd { get; init; }
}

public sealed record EventSessionStartRefusal
{
    public required IReadOnlyCollection<EventSessionStartGuard> ViolatedGuards { get; init; }
    public required int NonFinalTicketCount { get; init; }
    public required int UnansweredUnknownCount { get; init; }
    public required IReadOnlyCollection<ProductionLocation> LocationsOnTestPrinter { get; init; }
}

public enum EventSessionStartGuard
{
    NonFinalTicketsRemain,
    UnansweredUnknownQuestionsRemain,
    RecentOrderNeedsTypedConfirmation,
    ActiveLocationOnTestPrinter,
}
