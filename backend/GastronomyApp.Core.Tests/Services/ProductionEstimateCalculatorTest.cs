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
  public void SumQueuedMinutes_WorkWithAndWithoutAMinuteCount_SumsTheStatedMinutes()
  {
    var minutes = _calculator.SumQueuedMinutes([new(5, false), new(7, false)]);

    Assert.That(minutes, Is.EqualTo(12));
  }

  [Test]
  public void SumQueuedMinutes_HalfMinutes_SumsTheHalves()
  {
    var minutes = _calculator.SumQueuedMinutes([new(1.5, false), new(2.5, false)]);

    Assert.That(minutes, Is.EqualTo(4));
  }

  [Test]
  public void SumQueuedMinutes_FractionsThatCarryBinaryDust_ReachesTheWireWithOneDecimalPlace()
  {
    var minutes = _calculator.SumQueuedMinutes([new(0.1, false), new(0.2, false)]);

    Assert.That(minutes, Is.EqualTo(0.3));
  }

  [Test]
  public void SumQueuedMinutes_WorkWithoutAStatedMinuteCount_CountsAsZero()
  {
    var minutes = _calculator.SumQueuedMinutes([new(null, false), new(3, false)]);

    Assert.That(minutes, Is.EqualTo(3));
  }

  [Test]
  public void SumQueuedMinutes_WorkThatIsPreparedIndependently_StaysOutOfTheQueue()
  {
    var minutes = _calculator.SumQueuedMinutes([new(5, false), new(7, true)]);

    Assert.That(minutes, Is.EqualTo(5));
  }

  [Test]
  public void SumQueuedMinutes_HalfMinutesThatArePreparedIndependently_StayOutOfTheQueue()
  {
    var minutes = _calculator.SumQueuedMinutes([new(1.5, false), new(2.5, true)]);

    Assert.That(minutes, Is.EqualTo(1.5));
  }

  [Test]
  public void SumQueuedMinutes_OnlyIndependentWork_LeavesTheQueueEmpty()
  {
    var minutes = _calculator.SumQueuedMinutes([new(4, true), new(null, true)]);

    Assert.That(minutes, Is.Zero);
  }

  [Test]
  public void SumQueuedMinutes_NoWork_IsZero()
  {
    Assert.That(_calculator.SumQueuedMinutes([]), Is.Zero);
  }
}
