using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class GiveUpWindowCalculator
{
    public static readonly TimeSpan GiveUpWindow = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan OuterBound = TimeSpan.FromMinutes(20);

    public GiveUpWindowEvaluation Evaluate(
        DateTime printJobCreatedAtUtc,
        DateTime evaluatedAtUtc,
        PrintJobStatus currentStatus,
        IReadOnlyCollection<SuspensionPeriod> suspensionPeriods)
    {
        TimeSpan elapsed = evaluatedAtUtc - printJobCreatedAtUtc;
        TimeSpan suspended = TimeSpan.Zero;

        foreach (SuspensionPeriod period in suspensionPeriods)
        {
            DateTime effectiveStart = period.StartedAtUtc < printJobCreatedAtUtc
                ? printJobCreatedAtUtc
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
            HasReachedOuterBound = currentStatus != PrintJobStatus.Sending && elapsed >= OuterBound,
            AccumulatedUnsuspendedTime = accumulatedUnsuspendedTime,
        };
    }
}
