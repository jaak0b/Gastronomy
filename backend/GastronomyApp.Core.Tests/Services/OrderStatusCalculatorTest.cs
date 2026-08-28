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
            _calculator.Calculate([PrintJobStatus.Printed, PrintJobStatus.Unknown]),
            Is.EqualTo(OrderStatus.NeedsAttention));
    }

    [Test]
    public void Calculate_AnyTicketFailed_IsNeedsAttention()
    {
        Assert.That(
            _calculator.Calculate(
                [PrintJobStatus.Sending, PrintJobStatus.Failed, PrintJobStatus.Queued]),
            Is.EqualTo(OrderStatus.NeedsAttention));
    }

    [Test]
    public void Calculate_AnyTicketBlocked_IsNeedsAttention()
    {
        Assert.That(
            _calculator.Calculate([PrintJobStatus.Queued, PrintJobStatus.Blocked]),
            Is.EqualTo(OrderStatus.NeedsAttention));
    }

    [Test]
    public void Calculate_EveryTicketPrintedOrHandledOnPaper_IsPrinted()
    {
        Assert.That(
            _calculator.Calculate(
                [PrintJobStatus.Printed, PrintJobStatus.HandledOnPaper]),
            Is.EqualTo(OrderStatus.Printed));
    }

    [Test]
    public void Calculate_AnyTicketPrintingWithNoAttentionRow_IsPrinting()
    {
        Assert.That(
            _calculator.Calculate(
                [PrintJobStatus.Queued, PrintJobStatus.Sending, PrintJobStatus.Printed]),
            Is.EqualTo(OrderStatus.Printing));
    }

    [Test]
    public void Calculate_AtLeastOneQueuedAndNothingElseMatching_IsAccepted()
    {
        Assert.That(
            _calculator.Calculate([PrintJobStatus.Queued, PrintJobStatus.Printed]),
            Is.EqualTo(OrderStatus.Accepted));
    }

    [Test]
    public void Calculate_SingleQueuedTicket_IsAccepted()
    {
        Assert.That(
            _calculator.Calculate([PrintJobStatus.Queued]),
            Is.EqualTo(OrderStatus.Accepted));
    }

    private OrderStatus ExpectedByTable(IReadOnlyCollection<PrintJobStatus> statuses)
    {
        foreach (PrintJobStatus status in statuses)
        {
            if (status == PrintJobStatus.Unknown
                || status == PrintJobStatus.Failed
                || status == PrintJobStatus.Blocked)
            {
                return OrderStatus.NeedsAttention;
            }
        }

        bool everyTicketIsDone = true;
        foreach (PrintJobStatus status in statuses)
        {
            if (status != PrintJobStatus.Printed
                && status != PrintJobStatus.HandledOnPaper)
            {
                everyTicketIsDone = false;
            }
        }

        if (everyTicketIsDone)
        {
            return OrderStatus.Printed;
        }

        foreach (PrintJobStatus status in statuses)
        {
            if (status == PrintJobStatus.Sending)
            {
                return OrderStatus.Printing;
            }
        }

        return OrderStatus.Accepted;
    }

    [Test]
    public void Calculate_EverySingleTicketCombination_MatchesTheFirstMatchingRow()
    {
        PrintJobStatus[] allStatuses = Enum.GetValues<PrintJobStatus>();
        int checkedCombinations = 0;

        foreach (PrintJobStatus status in allStatuses)
        {
            List<PrintJobStatus> statuses = [status];

            Assert.That(
                _calculator.Calculate(statuses),
                Is.EqualTo(ExpectedByTable(statuses)),
                $"status {status}");
            checkedCombinations++;
        }

        Assert.That(checkedCombinations, Is.EqualTo(7));
    }

    [Test]
    public void Calculate_EveryTwoTicketCombination_MatchesTheFirstMatchingRow()
    {
        PrintJobStatus[] allStatuses = Enum.GetValues<PrintJobStatus>();
        int checkedCombinations = 0;

        foreach (PrintJobStatus first in allStatuses)
        {
            foreach (PrintJobStatus second in allStatuses)
            {
                List<PrintJobStatus> statuses = [first, second];

                Assert.That(
                    _calculator.Calculate(statuses),
                    Is.EqualTo(ExpectedByTable(statuses)),
                    $"statuses {first} and {second}");
                checkedCombinations++;
            }
        }

        Assert.That(checkedCombinations, Is.EqualTo(49));
    }
}
