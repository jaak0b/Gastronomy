using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class GiveUpWindowCalculatorTest
{
    private readonly DateTime _createdAtUtc = new(2026, 8, 26, 18, 0, 0, DateTimeKind.Utc);

    private GiveUpWindowCalculator _calculator = new();

    [SetUp]
    public void SetUp()
    {
        _calculator = new GiveUpWindowCalculator();
    }

    private DateTime AtMinute(double minute)
    {
        return _createdAtUtc.AddMinutes(minute);
    }

    private SuspensionPeriod SuspendedFrom(double startMinute, double? endMinute)
    {
        return new SuspensionPeriod
        {
            StartedAtUtc = AtMinute(startMinute),
            EndedAtUtc = endMinute is null ? null : AtMinute(endMinute.Value),
        };
    }

    private GiveUpWindowEvaluation EvaluateAt(
        double minute,
        PrintJobStatus currentStatus,
        IReadOnlyCollection<SuspensionPeriod> suspensionPeriods)
    {
        return _calculator.Evaluate(_createdAtUtc, AtMinute(minute), currentStatus, suspensionPeriods);
    }

    [Test]
    public void Evaluate_NoSuspensionAtFiveMinutes_HasReachedTheGiveUpWindow()
    {
        GiveUpWindowEvaluation evaluation = EvaluateAt(5, PrintJobStatus.Queued, []);

        Assert.Multiple(() =>
        {
            Assert.That(evaluation.HasReachedGiveUpWindow, Is.True);
            Assert.That(evaluation.HasReachedOuterBound, Is.False);
            Assert.That(evaluation.AccumulatedUnsuspendedTime, Is.EqualTo(TimeSpan.FromMinutes(5)));
        });
    }

    [Test]
    public void Evaluate_NoSuspensionAMomentBeforeFiveMinutes_HasNotReachedTheGiveUpWindow()
    {
        GiveUpWindowEvaluation evaluation = EvaluateAt(4.99, PrintJobStatus.Queued, []);

        Assert.That(evaluation.HasReachedGiveUpWindow, Is.False);
    }

    [Test]
    public void Evaluate_SuspendedForItsWholeLife_AccumulatesNothingAndDoesNotGiveUp()
    {
        GiveUpWindowEvaluation evaluation =
            EvaluateAt(12, PrintJobStatus.Blocked, [SuspendedFrom(0, null)]);

        Assert.Multiple(() =>
        {
            Assert.That(evaluation.AccumulatedUnsuspendedTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(evaluation.HasReachedGiveUpWindow, Is.False);
            Assert.That(evaluation.HasReachedOuterBound, Is.False);
        });
    }

    [Test]
    public void Evaluate_SuspendedForItsWholeLifeAtTwentyMinutes_HasReachedTheOuterBound()
    {
        GiveUpWindowEvaluation evaluation =
            EvaluateAt(20, PrintJobStatus.Blocked, [SuspendedFrom(0, null)]);

        Assert.Multiple(() =>
        {
            Assert.That(evaluation.AccumulatedUnsuspendedTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(evaluation.HasReachedGiveUpWindow, Is.False);
            Assert.That(evaluation.HasReachedOuterBound, Is.True);
        });
    }

    [Test]
    public void Evaluate_TheWorkedExampleAtTwentyMinutes_EndsAtTheOuterBoundWithTheStopwatchStillRunning()
    {
        GiveUpWindowEvaluation evaluation =
            EvaluateAt(20, PrintJobStatus.Queued, [SuspendedFrom(0, 18)]);

        Assert.Multiple(() =>
        {
            Assert.That(evaluation.AccumulatedUnsuspendedTime, Is.EqualTo(TimeSpan.FromMinutes(2)));
            Assert.That(evaluation.HasReachedGiveUpWindow, Is.False);
            Assert.That(evaluation.HasReachedOuterBound, Is.True);
        });
    }

    [Test]
    public void Evaluate_TheWorkedExampleBeforeTheOuterBound_HasNotYetReachedTheGiveUpWindow()
    {
        GiveUpWindowEvaluation evaluation =
            EvaluateAt(19, PrintJobStatus.Queued, [SuspendedFrom(0, 18)]);

        Assert.Multiple(() =>
        {
            Assert.That(evaluation.AccumulatedUnsuspendedTime, Is.EqualTo(TimeSpan.FromMinutes(1)));
            Assert.That(evaluation.HasReachedGiveUpWindow, Is.False);
            Assert.That(evaluation.HasReachedOuterBound, Is.False);
        });
    }

    [Test]
    public void Evaluate_TwoSeparateSuspensions_SumsTheUnsuspendedTimeWithoutResetting()
    {
        GiveUpWindowEvaluation evaluation = EvaluateAt(
            14,
            PrintJobStatus.Queued,
            [SuspendedFrom(0, 2), SuspendedFrom(10, 12)]);

        Assert.Multiple(() =>
        {
            Assert.That(evaluation.AccumulatedUnsuspendedTime, Is.EqualTo(TimeSpan.FromMinutes(10)));
            Assert.That(evaluation.HasReachedGiveUpWindow, Is.True);
        });
    }

    [Test]
    public void Evaluate_TwoSeparateSuspensionsBeforeFiveUnsuspendedMinutes_HasNotGivenUp()
    {
        GiveUpWindowEvaluation evaluation = EvaluateAt(
            8,
            PrintJobStatus.Queued,
            [SuspendedFrom(0, 2), SuspendedFrom(4, 6)]);

        Assert.Multiple(() =>
        {
            Assert.That(evaluation.AccumulatedUnsuspendedTime, Is.EqualTo(TimeSpan.FromMinutes(4)));
            Assert.That(evaluation.HasReachedGiveUpWindow, Is.False);
        });
    }

    [Test]
    public void Evaluate_AnySuspensionInForce_StopsTheClockWhicheverOfTheFourCausesRaisedIt()
    {
        GiveUpWindowEvaluation evaluation =
            EvaluateAt(6, PrintJobStatus.Blocked, [SuspendedFrom(0, null)]);

        Assert.Multiple(() =>
        {
            Assert.That(evaluation.AccumulatedUnsuspendedTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(evaluation.HasReachedGiveUpWindow, Is.False);
        });
    }

    [Test]
    public void Evaluate_TakesSuspensionPeriodsAsGiven_BecauseChoosingWhichCauseSuspendsIsTheCallersConcern()
    {
        GiveUpWindowEvaluation suspended =
            EvaluateAt(6, PrintJobStatus.Blocked, [SuspendedFrom(0, null)]);
        GiveUpWindowEvaluation notSuspended = EvaluateAt(6, PrintJobStatus.Blocked, []);

        Assert.Multiple(() =>
        {
            Assert.That(suspended.HasReachedGiveUpWindow, Is.False);
            Assert.That(notSuspended.HasReachedGiveUpWindow, Is.True);
        });
    }

    [Test]
    public void Evaluate_ABlockedTicketWithNoSuspendingCause_GivesUpAtFiveMinutes()
    {
        GiveUpWindowEvaluation evaluation = EvaluateAt(5, PrintJobStatus.Blocked, []);

        Assert.Multiple(() =>
        {
            Assert.That(evaluation.AccumulatedUnsuspendedTime, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(evaluation.HasReachedGiveUpWindow, Is.True);
        });
    }

    [Test]
    public void Evaluate_AtTwentyMinutesUnderEveryCause_HasReachedTheOuterBound()
    {
        List<IReadOnlyCollection<SuspensionPeriod>> everyCause =
        [
            [],
            [SuspendedFrom(0, null)],
            [SuspendedFrom(0, 18)],
            [SuspendedFrom(0, 2), SuspendedFrom(10, 12)],
        ];

        foreach (IReadOnlyCollection<SuspensionPeriod> suspensionPeriods in everyCause)
        {
            Assert.That(
                EvaluateAt(20, PrintJobStatus.Queued, suspensionPeriods).HasReachedOuterBound,
                Is.True);
            Assert.That(
                EvaluateAt(20, PrintJobStatus.Blocked, suspensionPeriods).HasReachedOuterBound,
                Is.True);
        }
    }

    [Test]
    public void Evaluate_ATicketStillPrintingWellPastTheOuterBound_HasNotReachedTheOuterBound()
    {
        GiveUpWindowEvaluation evaluation = EvaluateAt(25, PrintJobStatus.Sending, []);

        Assert.Multiple(() =>
        {
            Assert.That(evaluation.HasReachedOuterBound, Is.False);
            Assert.That(evaluation.AccumulatedUnsuspendedTime, Is.EqualTo(TimeSpan.FromMinutes(25)));
        });
    }

    [Test]
    public void Evaluate_ASuspensionStartingBeforeTheTicketExisted_CountsOnlyFromTicketCreation()
    {
        GiveUpWindowEvaluation evaluation = _calculator.Evaluate(
            _createdAtUtc,
            AtMinute(10),
            PrintJobStatus.Queued,
            [
                new SuspensionPeriod
                {
                    StartedAtUtc = _createdAtUtc.AddMinutes(-30),
                    EndedAtUtc = AtMinute(4),
                },
            ]);

        Assert.That(evaluation.AccumulatedUnsuspendedTime, Is.EqualTo(TimeSpan.FromMinutes(6)));
    }

    [Test]
    public void Evaluate_ASuspensionEndingAfterTheEvaluationMoment_IsClampedToThatMoment()
    {
        GiveUpWindowEvaluation evaluation = EvaluateAt(
            10,
            PrintJobStatus.Queued,
            [SuspendedFrom(4, 30)]);

        Assert.That(evaluation.AccumulatedUnsuspendedTime, Is.EqualTo(TimeSpan.FromMinutes(4)));
    }
}
