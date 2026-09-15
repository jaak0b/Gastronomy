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
    var minutes = _calculator.QueuedMinutesOf([new(5, false), new(7, false)]);

    Assert.That(minutes, Is.EqualTo(12));
  }

  [Test]
  public void QueuedMinutesOf_WorkWithoutAStatedMinuteCount_CountsAsZero()
  {
    var minutes = _calculator.QueuedMinutesOf([new(null, false), new(3, false)]);

    Assert.That(minutes, Is.EqualTo(3));
  }

  [Test]
  public void QueuedMinutesOf_WorkThatIsPreparedIndependently_StaysOutOfTheQueue()
  {
    var minutes = _calculator.QueuedMinutesOf([new(5, false), new(7, true)]);

    Assert.That(minutes, Is.EqualTo(5));
  }

  [Test]
  public void QueuedMinutesOf_OnlyIndependentWork_LeavesTheQueueEmpty()
  {
    var minutes = _calculator.QueuedMinutesOf([new(4, true), new(null, true)]);

    Assert.That(minutes, Is.Zero);
  }

  [Test]
  public void QueuedMinutesOf_NoWork_IsZero()
  {
    Assert.That(_calculator.QueuedMinutesOf([]), Is.Zero);
  }
}
