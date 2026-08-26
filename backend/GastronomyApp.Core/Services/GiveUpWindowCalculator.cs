using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class GiveUpWindowCalculator
{
    public static readonly TimeSpan GiveUpWindow = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan OuterBound = TimeSpan.FromMinutes(20);

    public GiveUpWindowEvaluation Evaluate(
        DateTime ticketCreatedAtUtc,
        DateTime evaluatedAtUtc,
        LocationTicketStatus currentStatus,
        IReadOnlyCollection<SuspensionPeriod> suspensionPeriods)
    {
        TimeSpan elapsed = evaluatedAtUtc - ticketCreatedAtUtc;
        TimeSpan suspended = TimeSpan.Zero;

        foreach (SuspensionPeriod period in suspensionPeriods)
        {
            DateTime effectiveStart = period.StartedAtUtc < ticketCreatedAtUtc
                ? ticketCreatedAtUtc
                : period.StartedAtUtc;
            DateTime effectiveEnd = period.EndedAtUtc is null || period.EndedAtUtc > evaluatedAtUtc
                ? evaluatedAtUtc
                : period.EndedAtUtc.Value;

            if (effectiveEnd > effectiveStart)
            {
                suspended += effectiveEnd - effectiveStart;
            }
        }

        TimeSpan accumulatedUnsuspendedTime = elapsed - suspended;
        if (accumulatedUnsuspendedTime < TimeSpan.Zero)
        {
            accumulatedUnsuspendedTime = TimeSpan.Zero;
        }

        return new GiveUpWindowEvaluation
        {
            HasReachedGiveUpWindow = accumulatedUnsuspendedTime >= GiveUpWindow,
            HasReachedOuterBound = currentStatus != LocationTicketStatus.Printing && elapsed >= OuterBound,
            AccumulatedUnsuspendedTime = accumulatedUnsuspendedTime,
        };
    }
}
