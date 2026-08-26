using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderTotalCalculatorTest
{
    private OrderTotalCalculator _calculator = new();

    [SetUp]
    public void SetUp()
    {
        _calculator = new OrderTotalCalculator();
    }

    private OrderLine LineWith(int quantity, int unitPriceCents, Guid locationTicketId)
    {
        return new OrderLine
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            LocationTicketId = locationTicketId,
            CatalogItemId = Guid.NewGuid(),
            ItemNameSnapshot = "Bratwurst",
            UnitPriceCentsSnapshot = unitPriceCents,
            Quantity = quantity,
        };
    }

    [Test]
    public void CalculateTotalCents_SingleLine_MultipliesQuantityByUnitPrice()
    {
        List<OrderLine> lines = [LineWith(2, 350, Guid.NewGuid())];

        Assert.That(_calculator.CalculateTotalCents(lines), Is.EqualTo(700));
    }

    [Test]
    public void CalculateTotalCents_MultipleLines_SumsEveryLine()
    {
        List<OrderLine> lines =
        [
            LineWith(2, 350, Guid.NewGuid()),
            LineWith(1, 400, Guid.NewGuid()),
            LineWith(3, 150, Guid.NewGuid()),
        ];

        Assert.That(_calculator.CalculateTotalCents(lines), Is.EqualTo(1550));
    }

    [Test]
    public void CalculateTotalCents_ZeroPricedLine_ContributesNothing()
    {
        List<OrderLine> lines =
        [
            LineWith(1, 400, Guid.NewGuid()),
            LineWith(4, 0, Guid.NewGuid()),
        ];

        Assert.That(_calculator.CalculateTotalCents(lines), Is.EqualTo(400));
    }

    [Test]
    public void CalculateTotalCents_LargeQuantity_MultipliesWithoutOverflow()
    {
        List<OrderLine> lines = [LineWith(99, 250, Guid.NewGuid())];

        Assert.That(_calculator.CalculateTotalCents(lines), Is.EqualTo(24750));
    }

    [Test]
    public void CalculateTotalCents_LinesAcrossDifferentTickets_TotalIsOrderWide()
    {
        Guid firstTicketId = Guid.NewGuid();
        Guid secondTicketId = Guid.NewGuid();
        List<OrderLine> lines =
        [
            LineWith(2, 350, firstTicketId),
            LineWith(1, 400, secondTicketId),
            LineWith(3, 150, firstTicketId),
        ];

        Assert.That(_calculator.CalculateTotalCents(lines), Is.EqualTo(1550));
    }

    [Test]
    public void CalculateTotalCents_NoLines_IsZero()
    {
        Assert.That(_calculator.CalculateTotalCents([]), Is.EqualTo(0));
    }
}
