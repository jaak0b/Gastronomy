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
  public void QueuedMinutesOf_WorkWithAndWithoutAMinuteCount_SumsTheStatedMinutes()
  {
    var minutes = _calculator.QueuedMinutesOf([new(5), new(7)]);

    Assert.That(minutes, Is.EqualTo(12));
  }

  [Test]
  public void QueuedMinutesOf_WorkWithoutAStatedMinuteCount_CountsAsZero()
  {
    var minutes = _calculator.QueuedMinutesOf([new(null), new(3)]);

    Assert.That(minutes, Is.EqualTo(3));
  }

  [Test]
  public void QueuedMinutesOf_NoWork_IsZero()
  {
    Assert.That(_calculator.QueuedMinutesOf([]), Is.Zero);
  }
}
