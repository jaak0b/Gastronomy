using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class ProductionEstimateCalculatorTest
{
  [SetUp]
  public void SetUp()
  {
    _calculator = new();
  }

  private ProductionEstimateCalculator _calculator = new();

  [Test]
  public void QueuedMinutesOf_WaitingAndInProductionItems_SumsTheirMinutes()
  {
    var minutes = _calculator.QueuedMinutesOf([
                                                new(ProductionStatus.Waiting, 5),
                                                new(ProductionStatus.InProduction, 7)
                                              ]);

    Assert.That(minutes, Is.EqualTo(12));
  }

  [Test]
  public void QueuedMinutesOf_FinishedItems_CountNothing()
  {
    var minutes = _calculator.QueuedMinutesOf([
                                                new(ProductionStatus.Finished, 30),
                                                new(ProductionStatus.Waiting, 4)
                                              ]);

    Assert.That(minutes, Is.EqualTo(4));
  }

  [Test]
  public void QueuedMinutesOf_ItemsWithoutAConfiguredDuration_CountAsZero()
  {
    var minutes = _calculator.QueuedMinutesOf([
                                                new(ProductionStatus.Waiting, null),
                                                new(ProductionStatus.Waiting, 3)
                                              ]);

    Assert.That(minutes, Is.EqualTo(3));
  }

  [Test]
  public void QueuedMinutesOf_NoItems_IsZero()
  {
    Assert.That(_calculator.QueuedMinutesOf([]), Is.Zero);
  }
}
