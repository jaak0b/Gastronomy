namespace GastronomyApp.Core.Results;

public sealed record RoutingDecision
{
    public required Guid ResolvedProductionLocationId { get; init; }
    public required Guid? ChosenProductionLocationId { get; init; }
    public required bool FellBackFromStaleChoice { get; init; }
}

public sealed record RoutingFailure
{
    public required RoutingFailureReason Reason { get; init; }
}

public enum RoutingFailureReason
{
    ItemHasNoStation,
    StationRequired,
    StationNotAssignedToItem,
}
