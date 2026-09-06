using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class ProductionStatusTransitionTest
{
  [SetUp]
  public void SetUp()
  {
    _transition = new();
  }

  private ProductionStatusTransition _transition = new();

  [TestCase(ProductionStatus.Waiting, ProductionStatus.InProduction)]
  [TestCase(ProductionStatus.Waiting, ProductionStatus.Finished)]
  [TestCase(ProductionStatus.InProduction, ProductionStatus.Finished)]
  public void IsAllowed_AStepForward_IsAllowed(ProductionStatus from, ProductionStatus to)
  {
    Assert.That(_transition.IsAllowed(from, to), Is.True);
  }

  [TestCase(ProductionStatus.InProduction, ProductionStatus.Waiting)]
  [TestCase(ProductionStatus.Finished, ProductionStatus.Waiting)]
  [TestCase(ProductionStatus.Finished, ProductionStatus.InProduction)]
  public void IsAllowed_AStepBackwards_IsRefused(ProductionStatus from, ProductionStatus to)
  {
    Assert.That(_transition.IsAllowed(from, to), Is.False);
  }

  [TestCase(ProductionStatus.Waiting)]
  [TestCase(ProductionStatus.InProduction)]
  [TestCase(ProductionStatus.Finished)]
  public void IsAllowed_StayingWhereItIs_IsRefused(ProductionStatus status)
  {
    Assert.That(_transition.IsAllowed(status, status), Is.False);
  }

  [TestCase(ProductionStatus.Waiting, ProductionStatus.InProduction)]
  [TestCase(ProductionStatus.Waiting, ProductionStatus.Finished)]
  [TestCase(ProductionStatus.InProduction, ProductionStatus.Finished)]
  public void StepFrom_AStepForward_IsForward(ProductionStatus from, ProductionStatus to)
  {
    Assert.That(_transition.StepFrom(from, to), Is.EqualTo(ProductionStatusStep.Forward));
  }

  [TestCase(ProductionStatus.Waiting)]
  [TestCase(ProductionStatus.InProduction)]
  [TestCase(ProductionStatus.Finished)]
  public void StepFrom_TheStatusItAlreadyHolds_IsAlreadyThere(ProductionStatus status)
  {
    Assert.That(_transition.StepFrom(status, status), Is.EqualTo(ProductionStatusStep.AlreadyThere));
  }

  [TestCase(ProductionStatus.InProduction, ProductionStatus.Waiting)]
  [TestCase(ProductionStatus.Finished, ProductionStatus.Waiting)]
  [TestCase(ProductionStatus.Finished, ProductionStatus.InProduction)]
  public void StepFrom_AStepBackwards_IsBackwards(ProductionStatus from, ProductionStatus to)
  {
    Assert.That(_transition.StepFrom(from, to), Is.EqualTo(ProductionStatusStep.Backwards));
  }

  [Test]
  public void IsAllowed_EveryPairOfStatuses_IsDecidedByWhetherItMovesForward()
  {
    ProductionStatus[] allStatuses = Enum.GetValues<ProductionStatus>();
    var checkedPairs = 0;

    foreach (var from in allStatuses)
    {
      foreach (var to in allStatuses)
      {
        Assert.That(_transition.IsAllowed(from, to), Is.EqualTo(to > from), $"{from} to {to}");
        checkedPairs++;
      }
    }

    Assert.That(checkedPairs, Is.EqualTo(9));
  }
}
