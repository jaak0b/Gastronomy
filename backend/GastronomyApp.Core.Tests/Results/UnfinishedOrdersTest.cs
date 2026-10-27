using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Tests.Results;

[TestFixture]
public sealed class UnfinishedOrdersTest
{
  [Test]
  public void Progress_NoUnfinishedOrder_IsAll()
  {
    Assert.That(new UnfinishedOrders(0, 0).Progress, Is.EqualTo(ShareState.All));
  }

  [Test]
  public void Progress_OnePartlyDoneOrderAmongTheUnfinishedOnes_IsSome()
  {
    Assert.That(new UnfinishedOrders(3, 1).Progress, Is.EqualTo(ShareState.Some));
  }

  [Test]
  public void Progress_UnfinishedOrdersOfWhichNoneIsStarted_IsNone()
  {
    Assert.That(new UnfinishedOrders(2, 0).Progress, Is.EqualTo(ShareState.None));
  }
}
