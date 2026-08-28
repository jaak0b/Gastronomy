using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class RetryPolicyTest
{
    private RetryPolicy _retryPolicy = new();

    [SetUp]
    public void SetUp()
    {
        _retryPolicy = new RetryPolicy();
    }

    private void AssertMapping(
        PrintOutcomeMapping mapping,
        PrintJobStatus expectedJobStatus,
        PrintJobStatus expectedTicketStatus,
        bool expectedShouldRetryAutomatically)
    {
        Assert.Multiple(() =>
        {
            Assert.That(mapping.JobStatus, Is.EqualTo(expectedJobStatus));
            Assert.That(mapping.ShouldRetryAutomatically, Is.EqualTo(expectedShouldRetryAutomatically));
            Assert.That(mapping.FailureReason, Is.Null);
        });
    }

    [Test]
    public void Map_ConfirmedAfterBytesReachedThePrinter_IsConfirmedAndPrinted()
    {
        AssertMapping(
            _retryPolicy.Map(PrintOutcome.Confirmed, 512),
            PrintJobStatus.Printed,
            PrintJobStatus.Printed,
            false);
    }

    [Test]
    public void Map_BlockedWithNoBytesWritten_IsBlockedAndRetriedWhenTheConditionClears()
    {
        AssertMapping(
            _retryPolicy.Map(PrintOutcome.Blocked, 0),
            PrintJobStatus.Blocked,
            PrintJobStatus.Blocked,
            true);
    }

    [Test]
    public void Map_UnreachableWithNoBytesWritten_IsQueuedAndRetried()
    {
        AssertMapping(
            _retryPolicy.Map(PrintOutcome.Unreachable, 0),
            PrintJobStatus.Queued,
            PrintJobStatus.Queued,
            true);
    }

    [Test]
    public void Map_SocketDroppedWithNoBytesWritten_IsQueuedAndRetried()
    {
        AssertMapping(
            _retryPolicy.Map(PrintOutcome.SocketDropped, 0),
            PrintJobStatus.Queued,
            PrintJobStatus.Queued,
            true);
    }

    [Test]
    public void Map_SocketDroppedAfterBytesWereWritten_IsUnknownAndNeverRetried()
    {
        AssertMapping(
            _retryPolicy.Map(PrintOutcome.SocketDropped, 1),
            PrintJobStatus.Unknown,
            PrintJobStatus.Unknown,
            false);
    }

    [Test]
    public void Map_TimeoutWithNoBytesWritten_IsQueuedAndRetried()
    {
        AssertMapping(
            _retryPolicy.Map(PrintOutcome.Timeout, 0),
            PrintJobStatus.Queued,
            PrintJobStatus.Queued,
            true);
    }

    [Test]
    public void Map_TimeoutAfterBytesWereWritten_IsUnknownAndNeverRetried()
    {
        AssertMapping(
            _retryPolicy.Map(PrintOutcome.Timeout, 1),
            PrintJobStatus.Unknown,
            PrintJobStatus.Unknown,
            false);
    }

    [Test]
    public void Map_PrinterErrorWithNoBytesWritten_IsBlockedAndRetriedWhenTheErrorClears()
    {
        AssertMapping(
            _retryPolicy.Map(PrintOutcome.PrinterError, 0),
            PrintJobStatus.Blocked,
            PrintJobStatus.Blocked,
            true);
    }

    [Test]
    public void Map_PrinterErrorAfterBytesWereWritten_IsUnknownAndNeverRetried()
    {
        AssertMapping(
            _retryPolicy.Map(PrintOutcome.PrinterError, 1),
            PrintJobStatus.Unknown,
            PrintJobStatus.Unknown,
            false);
    }

    [Test]
    public void Map_EveryRowInTheTable_LeavesTheFailureReasonUnassigned()
    {
        List<PrintOutcomeMapping> everyRow =
        [
            _retryPolicy.Map(PrintOutcome.Confirmed, 512),
            _retryPolicy.Map(PrintOutcome.Blocked, 0),
            _retryPolicy.Map(PrintOutcome.Unreachable, 0),
            _retryPolicy.Map(PrintOutcome.SocketDropped, 0),
            _retryPolicy.Map(PrintOutcome.SocketDropped, 1),
            _retryPolicy.Map(PrintOutcome.Timeout, 0),
            _retryPolicy.Map(PrintOutcome.Timeout, 1),
            _retryPolicy.Map(PrintOutcome.PrinterError, 0),
            _retryPolicy.Map(PrintOutcome.PrinterError, 1),
        ];

        Assert.That(everyRow.Select(mapping => mapping.FailureReason), Is.All.Null);
    }

    [Test]
    public void Map_ConfirmedWithZeroBytesWritten_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => _retryPolicy.Map(PrintOutcome.Confirmed, 0));
    }

    [Test]
    public void Map_AZeroByteOnlyOutcomeReportedWithBytesWritten_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(
                () => _retryPolicy.Map(PrintOutcome.Blocked, 1));
            Assert.Throws<InvalidOperationException>(
                () => _retryPolicy.Map(PrintOutcome.Unreachable, 1));
        });
    }

    private Dictionary<PrintOutcome, bool> OutcomesTheTableDefinesWithBytesWritten()
    {
        return new Dictionary<PrintOutcome, bool>
        {
            [PrintOutcome.Confirmed] = true,
            [PrintOutcome.Blocked] = false,
            [PrintOutcome.Unreachable] = false,
            [PrintOutcome.SocketDropped] = true,
            [PrintOutcome.Timeout] = true,
            [PrintOutcome.PrinterError] = true,
        };
    }

    private Dictionary<PrintOutcome, bool> OutcomesTheTableDefinesWithNoBytesWritten()
    {
        return new Dictionary<PrintOutcome, bool>
        {
            [PrintOutcome.Confirmed] = false,
            [PrintOutcome.Blocked] = true,
            [PrintOutcome.Unreachable] = true,
            [PrintOutcome.SocketDropped] = true,
            [PrintOutcome.Timeout] = true,
            [PrintOutcome.PrinterError] = true,
        };
    }

    [Test]
    public void Map_TheOutcomeTableItself_CoversEveryDeclaredPrintAttemptOutcome()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                OutcomesTheTableDefinesWithBytesWritten().Keys,
                Is.EquivalentTo(Enum.GetValues<PrintOutcome>()));
            Assert.That(
                OutcomesTheTableDefinesWithNoBytesWritten().Keys,
                Is.EquivalentTo(Enum.GetValues<PrintOutcome>()));
        });
    }

    [Test]
    public void Map_EveryOutcomeAndByteCount_EitherMapsOrThrowsExactlyAsTheTableSays()
    {
        Dictionary<PrintOutcome, bool> withBytes = OutcomesTheTableDefinesWithBytesWritten();
        Dictionary<PrintOutcome, bool> withoutBytes = OutcomesTheTableDefinesWithNoBytesWritten();
        int checkedCombinations = 0;

        foreach (PrintOutcome outcome in Enum.GetValues<PrintOutcome>())
        {
            foreach (int bytesWritten in new[] { 0, 1, 512 })
            {
                bool isDefinedByTheTable = bytesWritten > 0
                    ? withBytes[outcome]
                    : withoutBytes[outcome];

                if (isDefinedByTheTable)
                {
                    Assert.That(
                        _retryPolicy.Map(outcome, bytesWritten),
                        Is.Not.Null,
                        $"{outcome} with {bytesWritten} bytes");
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(
                        () => _retryPolicy.Map(outcome, bytesWritten),
                        $"{outcome} with {bytesWritten} bytes");
                }

                checkedCombinations++;
            }
        }

        Assert.That(checkedCombinations, Is.EqualTo(6 * 3));
    }
}
