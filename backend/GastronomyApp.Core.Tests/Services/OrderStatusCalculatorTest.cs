using GastronomyApp.Core.Enums;
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
  public void Calculate_EveryItemWaiting_IsWaiting()
  {
    Assert.That(_calculator.Calculate([ProductionStatus.Waiting, ProductionStatus.Waiting]),
                Is.EqualTo(OrderStatus.Waiting));
  }

  [Test]
  public void Calculate_OneItemInProductionAndTheRestWaiting_IsInProduction()
  {
    Assert.That(_calculator.Calculate([ProductionStatus.Waiting, ProductionStatus.InProduction]),
                Is.EqualTo(OrderStatus.InProduction));
  }

  [Test]
  public void Calculate_OneItemFinishedAndTheRestWaiting_IsInProduction()
  {
    Assert.That(_calculator.Calculate([ProductionStatus.Finished, ProductionStatus.Waiting]),
                Is.EqualTo(OrderStatus.InProduction));
  }

  [Test]
  public void Calculate_EveryItemFinished_IsFinished()
  {
    Assert.That(_calculator.Calculate([ProductionStatus.Finished, ProductionStatus.Finished]),
                Is.EqualTo(OrderStatus.Finished));
  }

  [Test]
  public void Calculate_NoItemsAtAll_IsWaiting()
  {
    Assert.That(_calculator.Calculate([]), Is.EqualTo(OrderStatus.Waiting));
  }

  private OrderStatus ExpectedByTable(IReadOnlyCollection<ProductionStatus> statuses)
  {
    if (statuses.Count == 0)
    {
      return OrderStatus.Waiting;
    }

    var everyItemIsFinished = true;
    var everyItemIsWaiting = true;

    foreach (var status in statuses)
    {
      if (status != ProductionStatus.Finished)
      {
        everyItemIsFinished = false;
      }

      if (status != ProductionStatus.Waiting)
      {
        everyItemIsWaiting = false;
      }
    }

    if (everyItemIsFinished)
    {
      return OrderStatus.Finished;
    }

    return everyItemIsWaiting ? OrderStatus.Waiting : OrderStatus.InProduction;
  }

  [Test]
  public void Calculate_EveryTwoItemCombination_MatchesTheTable()
  {
    ProductionStatus[] allStatuses = Enum.GetValues<ProductionStatus>();
    var checkedCombinations = 0;

    foreach (var first in allStatuses)
    {
      foreach (var second in allStatuses)
      {
        List<ProductionStatus> statuses = [first, second];

        Assert.That(_calculator.Calculate(statuses),
                    Is.EqualTo(ExpectedByTable(statuses)),
                    $"statuses {first} and {second}");
        checkedCombinations++;
      }
    }

    Assert.That(checkedCombinations, Is.EqualTo(9));
  }
}
