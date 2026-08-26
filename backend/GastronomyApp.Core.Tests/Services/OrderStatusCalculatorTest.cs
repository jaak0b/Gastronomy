using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderStatusCalculatorTest
{
    private OrderStatusCalculator _calculator = new();

    [SetUp]
    public void SetUp()
    {
        _calculator = new OrderStatusCalculator();
    }

    [Test]
    public void Calculate_AnyTicketUnknown_IsNeedsAttention()
    {
        Assert.That(
            _calculator.Calculate([LocationTicketStatus.Printed, LocationTicketStatus.Unknown], false),
            Is.EqualTo(OrderStatus.NeedsAttention));
    }

    [Test]
    public void Calculate_AnyTicketFailed_IsNeedsAttention()
    {
        Assert.That(
            _calculator.Calculate(
                [LocationTicketStatus.Printing, LocationTicketStatus.Failed, LocationTicketStatus.Queued],
                false),
            Is.EqualTo(OrderStatus.NeedsAttention));
    }

    [Test]
    public void Calculate_AnyTicketBlocked_IsNeedsAttention()
    {
        Assert.That(
            _calculator.Calculate([LocationTicketStatus.Queued, LocationTicketStatus.Blocked], false),
            Is.EqualTo(OrderStatus.NeedsAttention));
    }

    [Test]
    public void Calculate_PrintedOnTestPrinterOutsideAPracticeSession_IsNeedsAttention()
    {
        Assert.That(
            _calculator.Calculate(
                [LocationTicketStatus.Printed, LocationTicketStatus.PrintedOnTestPrinter],
                false),
            Is.EqualTo(OrderStatus.NeedsAttention));
    }

    [Test]
    public void Calculate_PrintedOnTestPrinterInAPracticeSession_FallsThroughToPrinted()
    {
        Assert.That(
            _calculator.Calculate(
                [LocationTicketStatus.Printed, LocationTicketStatus.PrintedOnTestPrinter],
                true),
            Is.EqualTo(OrderStatus.Printed));
    }

    [Test]
    public void Calculate_EveryTicketPrintedOrHandledOnPaper_IsPrinted()
    {
        Assert.That(
            _calculator.Calculate(
                [LocationTicketStatus.Printed, LocationTicketStatus.HandledOnPaper],
                false),
            Is.EqualTo(OrderStatus.Printed));
    }

    [Test]
    public void Calculate_EveryTicketPrintedHandledOnPaperOrTestPrinterInAPracticeSession_IsPrinted()
    {
        Assert.That(
            _calculator.Calculate(
                [
                    LocationTicketStatus.Printed,
                    LocationTicketStatus.HandledOnPaper,
                    LocationTicketStatus.PrintedOnTestPrinter,
                ],
                true),
            Is.EqualTo(OrderStatus.Printed));
    }

    [Test]
    public void Calculate_AnyTicketPrintingWithNoAttentionRow_IsPrinting()
    {
        Assert.That(
            _calculator.Calculate(
                [LocationTicketStatus.Queued, LocationTicketStatus.Printing, LocationTicketStatus.Printed],
                false),
            Is.EqualTo(OrderStatus.Printing));
    }

    [Test]
    public void Calculate_AtLeastOneQueuedAndNothingElseMatching_IsAccepted()
    {
        Assert.That(
            _calculator.Calculate([LocationTicketStatus.Queued, LocationTicketStatus.Printed], false),
            Is.EqualTo(OrderStatus.Accepted));
    }

    [Test]
    public void Calculate_SingleQueuedTicket_IsAccepted()
    {
        Assert.That(
            _calculator.Calculate([LocationTicketStatus.Queued], false),
            Is.EqualTo(OrderStatus.Accepted));
    }

    private OrderStatus ExpectedByTable(IReadOnlyCollection<LocationTicketStatus> statuses, bool isPracticeSession)
    {
        foreach (LocationTicketStatus status in statuses)
        {
            if (status == LocationTicketStatus.Unknown
                || status == LocationTicketStatus.Failed
                || status == LocationTicketStatus.Blocked)
            {
                return OrderStatus.NeedsAttention;
            }
        }

        if (!isPracticeSession)
        {
            foreach (LocationTicketStatus status in statuses)
            {
                if (status == LocationTicketStatus.PrintedOnTestPrinter)
                {
                    return OrderStatus.NeedsAttention;
                }
            }
        }

        bool everyTicketIsDone = true;
        foreach (LocationTicketStatus status in statuses)
        {
            if (status != LocationTicketStatus.Printed
                && status != LocationTicketStatus.HandledOnPaper
                && status != LocationTicketStatus.PrintedOnTestPrinter)
            {
                everyTicketIsDone = false;
            }
        }

        if (everyTicketIsDone)
        {
            return OrderStatus.Printed;
        }

        foreach (LocationTicketStatus status in statuses)
        {
            if (status == LocationTicketStatus.Printing)
            {
                return OrderStatus.Printing;
            }
        }

        return OrderStatus.Accepted;
    }

    [Test]
    public void Calculate_EverySingleTicketCombination_MatchesTheFirstMatchingRow()
    {
        LocationTicketStatus[] allStatuses = Enum.GetValues<LocationTicketStatus>();
        int checkedCombinations = 0;

        foreach (LocationTicketStatus status in allStatuses)
        {
            foreach (bool isPracticeSession in new[] { false, true })
            {
                List<LocationTicketStatus> statuses = [status];

                Assert.That(
                    _calculator.Calculate(statuses, isPracticeSession),
                    Is.EqualTo(ExpectedByTable(statuses, isPracticeSession)),
                    $"status {status}, practice {isPracticeSession}");
                checkedCombinations++;
            }
        }

        Assert.That(checkedCombinations, Is.EqualTo(16));
    }

    [Test]
    public void Calculate_EveryTwoTicketCombination_MatchesTheFirstMatchingRow()
    {
        LocationTicketStatus[] allStatuses = Enum.GetValues<LocationTicketStatus>();
        int checkedCombinations = 0;

        foreach (LocationTicketStatus first in allStatuses)
        {
            foreach (LocationTicketStatus second in allStatuses)
            {
                List<LocationTicketStatus> statuses = [first, second];

                Assert.That(
                    _calculator.Calculate(statuses, false),
                    Is.EqualTo(ExpectedByTable(statuses, false)),
                    $"statuses {first} and {second}");
                checkedCombinations++;
            }
        }

        Assert.That(checkedCombinations, Is.EqualTo(64));
    }
}
