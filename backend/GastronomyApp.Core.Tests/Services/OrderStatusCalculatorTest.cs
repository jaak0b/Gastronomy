using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OrderStatusCalculatorTest
{
  [SetUp]
  public void SetUp()
  {
    _calculator = new();
  }

  private OrderStatusCalculator _calculator = new();

  [Test]
  public void Calculate_NothingFulfilled_IsOpen()
  {
    Assert.That(_calculator.Calculate(2, 0), Is.EqualTo(OrderStatus.Open));
  }

  [Test]
  public void Calculate_SomeItemsFulfilled_IsPartiallyFulfilled()
  {
    Assert.That(_calculator.Calculate(3, 1), Is.EqualTo(OrderStatus.PartiallyFulfilled));
  }

  [Test]
  public void Calculate_EveryItemFulfilled_IsFulfilled()
  {
    Assert.That(_calculator.Calculate(2, 2), Is.EqualTo(OrderStatus.Fulfilled));
  }

  [Test]
  public void Calculate_AnOrderWithoutItems_IsOpen()
  {
    Assert.That(_calculator.Calculate(0, 0), Is.EqualTo(OrderStatus.Open));
  }
}
