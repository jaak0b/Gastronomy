using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderTotalCalculatorTest
{
  [SetUp]
  public void SetUp()
  {
    _calculator = new();
  }

  private OrderTotalCalculator _calculator = null!;

  [Test]
  public void SumTotalCents_NullItems_ThrowsArgumentNullException()
  {
    Assert.That(() => _calculator.SumTotalCents(null!), Throws.ArgumentNullException);
  }

  [Test]
  public void SumTotalCents_AnOrderWithoutItems_CountsNothing()
  {
    Assert.That(_calculator.SumTotalCents([]), Is.Zero);
  }

  [Test]
  public void SumTotalCents_SeveralItems_AddsThePricesThePhoneDisplayed()
  {
    Assert.That(_calculator.SumTotalCents([Item(350), Item(400), Item(250)]), Is.EqualTo(1000));
  }

  private PlacedOrderItem Item(int unitPriceCents)
  {
    return new()
           {
             OrderItemId = Guid.NewGuid(),
             UnitPriceCents = unitPriceCents,
             IsFulfilled = false
           };
  }
}
