using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Tests.TestSupport;

namespace GastronomyApp.Core.Tests.Entities;

[TestFixture]
public sealed class StationOrderTest
{
  [Test]
  public void HideFromAsItComesQueue_ATogetherStationOrder_IsRefusedAndLeavesTheFlagOff()
  {
    var stationOrder = BuildStationOrder(DeliveryMode.Together);

    ErrorOr<StationOrder> outcome = stationOrder.HideFromAsItComesQueue();

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.False);
                      Assert.That(outcome.RefusalMessageKey(), Is.EqualTo("station.changeNotSaved"));
                      Assert.That(stationOrder.IsHiddenFromAsItComesQueue, Is.False);
                    });
  }

  [Test]
  public void HideFromAsItComesQueue_AnAsItComesStationOrder_SetsTheFlag()
  {
    var stationOrder = BuildStationOrder(DeliveryMode.AsItComes);

    ErrorOr<StationOrder> outcome = stationOrder.HideFromAsItComesQueue();

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(outcome.Value, Is.SameAs(stationOrder));
                      Assert.That(stationOrder.IsHiddenFromAsItComesQueue, Is.True);
                    });
  }

  [Test]
  public void HideFromAsItComesQueue_AnAlreadyHiddenStationOrder_IsAccepted()
  {
    var stationOrder = BuildStationOrder(DeliveryMode.AsItComes);
    stationOrder.IsHiddenFromAsItComesQueue = true;

    ErrorOr<StationOrder> outcome = stationOrder.HideFromAsItComesQueue();

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(stationOrder.IsHiddenFromAsItComesQueue, Is.True);
                    });
  }

  private StationOrder BuildStationOrder(DeliveryMode deliveryMode)
  {
    return new()
    {
      Id = Guid.NewGuid(),
      OrderId = Guid.NewGuid(),
      FestivalId = Guid.NewGuid(),
      StationId = Guid.NewGuid(),
      StationOrderNumber = 1,
      DeliveryMode = deliveryMode
    };
  }
}
