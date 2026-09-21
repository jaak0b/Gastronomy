using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Tests.Entities;

[TestFixture]
public sealed class StationOrderTest
{
  [Test]
  public void IsInAsItComesColumn_AnAsItComesOrderNobodyHid_StandsInTheColumn()
  {
    Assert.That(StationOrderWith(DeliveryMode.AsItComes, false).IsInAsItComesColumn(), Is.True);
  }

  [Test]
  public void IsInAsItComesColumn_AnAsItComesOrderTheEmployeeHid_StaysOutOfTheColumn()
  {
    Assert.That(StationOrderWith(DeliveryMode.AsItComes, true).IsInAsItComesColumn(), Is.False);
  }

  [Test]
  public void IsInAsItComesColumn_ATogetherOrder_StaysOutOfTheColumn()
  {
    Assert.That(StationOrderWith(DeliveryMode.Together, false).IsInAsItComesColumn(), Is.False);
  }

  [Test]
  public void IsInAsItComesColumn_ATogetherOrderMarkedHidden_StaysOutOfTheColumnAllTheSame()
  {
    Assert.That(StationOrderWith(DeliveryMode.Together, true).IsInAsItComesColumn(), Is.False);
  }

  private StationOrder StationOrderWith(DeliveryMode deliveryMode, bool isHidden)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             OrderId = Guid.NewGuid(),
             FestivalId = Guid.NewGuid(),
             StationId = Guid.NewGuid(),
             StationOrderNumber = 1,
             DeliveryMode = deliveryMode,
             IsHiddenFromAsItComesQueue = isHidden
           };
  }
}
