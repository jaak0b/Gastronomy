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
        LocationTicketStatus expectedTicketStatus,
        bool expectedShouldRetryAutomatically)
    {
        Assert.Multiple(() =>
        {
            Assert.That(mapping.JobStatus, Is.EqualTo(expectedJobStatus));
            Assert.That(mapping.TicketStatus, Is.EqualTo(expectedTicketStatus));
            Assert.That(mapping.ShouldRetryAutomatically, Is.EqualTo(expectedShouldRetryAutomatically));
            Assert.That(mapping.FailureReason, Is.Null);
        });
    }

    [Test]
    public void Map_ConfirmedOnANetworkTransport_IsConfirmedAndPrinted()
    {
        AssertMapping(
            _retryPolicy.Map(PrintAttemptOutcome.Confirmed, TransportKind.Network, 512),
            PrintJobStatus.Confirmed,
            LocationTicketStatus.Printed,
            false);
    }

    [Test]
    public void Map_ConfirmedOnAnAgentTransport_IsConfirmedAndPrinted()
    {
        AssertMapping(
            _retryPolicy.Map(PrintAttemptOutcome.Confirmed, TransportKind.Agent, 512),
            PrintJobStatus.Confirmed,
            LocationTicketStatus.Printed,
            false);
    }

    [Test]
    public void Map_ConfirmedOnAMockTransport_IsConfirmedAndPrintedOnTestPrinter()
    {
        AssertMapping(
            _retryPolicy.Map(PrintAttemptOutcome.Confirmed, TransportKind.Mock, 512),
            PrintJobStatus.Confirmed,
            LocationTicketStatus.PrintedOnTestPrinter,
            false);
    }

    [Test]
    public void Map_BlockedWithNoBytesWritten_IsBlockedAndRetriedWhenTheConditionClears()
    {
        AssertMapping(
            _retryPolicy.Map(PrintAttemptOutcome.Blocked, TransportKind.Network, 0),
            PrintJobStatus.Blocked,
            LocationTicketStatus.Blocked,
            true);
    }

    [Test]
    public void Map_UnreachableWithNoBytesWritten_IsQueuedAndRetried()
    {
        AssertMapping(
            _retryPolicy.Map(PrintAttemptOutcome.Unreachable, TransportKind.Network, 0),
            PrintJobStatus.Queued,
            LocationTicketStatus.Queued,
            true);
    }

    [Test]
    public void Map_SocketDroppedWithNoBytesWritten_IsQueuedAndRetried()
    {
        AssertMapping(
            _retryPolicy.Map(PrintAttemptOutcome.SocketDropped, TransportKind.Network, 0),
            PrintJobStatus.Queued,
            LocationTicketStatus.Queued,
            true);
    }

    [Test]
    public void Map_SocketDroppedAfterBytesWereWritten_IsUnknownAndNeverRetried()
    {
        AssertMapping(
            _retryPolicy.Map(PrintAttemptOutcome.SocketDropped, TransportKind.Network, 1),
            PrintJobStatus.Unknown,
            LocationTicketStatus.Unknown,
            false);
    }

    [Test]
    public void Map_TimeoutWithNoBytesWritten_IsQueuedAndRetried()
    {
        AssertMapping(
            _retryPolicy.Map(PrintAttemptOutcome.Timeout, TransportKind.Network, 0),
            PrintJobStatus.Queued,
            LocationTicketStatus.Queued,
            true);
    }

    [Test]
    public void Map_TimeoutAfterBytesWereWritten_IsUnknownAndNeverRetried()
    {
        AssertMapping(
            _retryPolicy.Map(PrintAttemptOutcome.Timeout, TransportKind.Network, 1),
            PrintJobStatus.Unknown,
            LocationTicketStatus.Unknown,
            false);
    }

    [Test]
    public void Map_PrinterErrorWithNoBytesWritten_IsBlockedAndRetriedWhenTheErrorClears()
    {
        AssertMapping(
            _retryPolicy.Map(PrintAttemptOutcome.PrinterError, TransportKind.Network, 0),
            PrintJobStatus.Blocked,
            LocationTicketStatus.Blocked,
            true);
    }

    [Test]
    public void Map_PrinterErrorAfterBytesWereWritten_IsUnknownAndNeverRetried()
    {
        AssertMapping(
            _retryPolicy.Map(PrintAttemptOutcome.PrinterError, TransportKind.Network, 1),
            PrintJobStatus.Unknown,
            LocationTicketStatus.Unknown,
            false);
    }

    [Test]
    public void Map_EveryFailureRow_IsIdenticalOnEveryTransport()
    {
        PrintAttemptOutcome[] failureOutcomes =
        [
            PrintAttemptOutcome.Blocked,
            PrintAttemptOutcome.Unreachable,
            PrintAttemptOutcome.SocketDropped,
            PrintAttemptOutcome.Timeout,
            PrintAttemptOutcome.PrinterError,
        ];

        foreach (PrintAttemptOutcome outcome in failureOutcomes)
        {
            PrintOutcomeMapping onNetwork = _retryPolicy.Map(outcome, TransportKind.Network, 0);

            Assert.Multiple(() =>
            {
                Assert.That(_retryPolicy.Map(outcome, TransportKind.Agent, 0), Is.EqualTo(onNetwork));
                Assert.That(_retryPolicy.Map(outcome, TransportKind.Mock, 0), Is.EqualTo(onNetwork));
            });
        }
    }

    [Test]
    public void Map_EveryRowInTheTable_LeavesTheFailureReasonUnassigned()
    {
        List<PrintOutcomeMapping> everyRow =
        [
            _retryPolicy.Map(PrintAttemptOutcome.Confirmed, TransportKind.Network, 512),
            _retryPolicy.Map(PrintAttemptOutcome.Confirmed, TransportKind.Mock, 512),
            _retryPolicy.Map(PrintAttemptOutcome.Blocked, TransportKind.Network, 0),
            _retryPolicy.Map(PrintAttemptOutcome.Unreachable, TransportKind.Network, 0),
            _retryPolicy.Map(PrintAttemptOutcome.SocketDropped, TransportKind.Network, 0),
            _retryPolicy.Map(PrintAttemptOutcome.SocketDropped, TransportKind.Network, 1),
            _retryPolicy.Map(PrintAttemptOutcome.Timeout, TransportKind.Network, 0),
            _retryPolicy.Map(PrintAttemptOutcome.Timeout, TransportKind.Network, 1),
            _retryPolicy.Map(PrintAttemptOutcome.PrinterError, TransportKind.Network, 0),
            _retryPolicy.Map(PrintAttemptOutcome.PrinterError, TransportKind.Network, 1),
        ];

        Assert.That(everyRow.Select(mapping => mapping.FailureReason), Is.All.Null);
    }

    [Test]
    public void Map_ConfirmedWithZeroBytesWritten_Throws()
    {
        foreach (TransportKind transportKind in Enum.GetValues<TransportKind>())
        {
            Assert.Throws<InvalidOperationException>(
                () => _retryPolicy.Map(PrintAttemptOutcome.Confirmed, transportKind, 0));
        }
    }

    [Test]
    public void Map_AZeroByteOnlyOutcomeReportedWithBytesWritten_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(
                () => _retryPolicy.Map(PrintAttemptOutcome.Blocked, TransportKind.Network, 1));
            Assert.Throws<InvalidOperationException>(
                () => _retryPolicy.Map(PrintAttemptOutcome.Unreachable, TransportKind.Network, 1));
        });
    }

    private Dictionary<PrintAttemptOutcome, bool> OutcomesTheTableDefinesWithBytesWritten()
    {
        return new Dictionary<PrintAttemptOutcome, bool>
        {
            [PrintAttemptOutcome.Confirmed] = true,
            [PrintAttemptOutcome.Blocked] = false,
            [PrintAttemptOutcome.Unreachable] = false,
            [PrintAttemptOutcome.SocketDropped] = true,
            [PrintAttemptOutcome.Timeout] = true,
            [PrintAttemptOutcome.PrinterError] = true,
        };
    }

    private Dictionary<PrintAttemptOutcome, bool> OutcomesTheTableDefinesWithNoBytesWritten()
    {
        return new Dictionary<PrintAttemptOutcome, bool>
        {
            [PrintAttemptOutcome.Confirmed] = false,
            [PrintAttemptOutcome.Blocked] = true,
            [PrintAttemptOutcome.Unreachable] = true,
            [PrintAttemptOutcome.SocketDropped] = true,
            [PrintAttemptOutcome.Timeout] = true,
            [PrintAttemptOutcome.PrinterError] = true,
        };
    }

    [Test]
    public void Map_TheOutcomeTableItself_CoversEveryDeclaredPrintAttemptOutcome()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                OutcomesTheTableDefinesWithBytesWritten().Keys,
                Is.EquivalentTo(Enum.GetValues<PrintAttemptOutcome>()));
            Assert.That(
                OutcomesTheTableDefinesWithNoBytesWritten().Keys,
                Is.EquivalentTo(Enum.GetValues<PrintAttemptOutcome>()));
        });
    }

    [Test]
    public void Map_EveryOutcomeAndByteCountAndTransport_EitherMapsOrThrowsExactlyAsTheTableSays()
    {
        Dictionary<PrintAttemptOutcome, bool> withBytes = OutcomesTheTableDefinesWithBytesWritten();
        Dictionary<PrintAttemptOutcome, bool> withoutBytes = OutcomesTheTableDefinesWithNoBytesWritten();
        int checkedCombinations = 0;

        foreach (PrintAttemptOutcome outcome in Enum.GetValues<PrintAttemptOutcome>())
        {
            foreach (TransportKind transportKind in Enum.GetValues<TransportKind>())
            {
                foreach (int bytesWritten in new[] { 0, 1, 512 })
                {
                    bool isDefinedByTheTable = bytesWritten > 0
                        ? withBytes[outcome]
                        : withoutBytes[outcome];

                    if (isDefinedByTheTable)
                    {
                        Assert.That(
                            _retryPolicy.Map(outcome, transportKind, bytesWritten),
                            Is.Not.Null,
                            $"{outcome} on {transportKind} with {bytesWritten} bytes");
                    }
                    else
                    {
                        Assert.Throws<InvalidOperationException>(
                            () => _retryPolicy.Map(outcome, transportKind, bytesWritten),
                            $"{outcome} on {transportKind} with {bytesWritten} bytes");
                    }

                    checkedCombinations++;
                }
            }
        }

        Assert.That(checkedCombinations, Is.EqualTo(6 * 3 * 3));
    }
}
