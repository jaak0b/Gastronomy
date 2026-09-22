using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class StationOrderServiceTest
{
  private readonly StationOrderService _service = new();

  [Test]
  public void HideFromAsItComesQueue_ATogetherStationOrder_IsRefusedAndLeavesTheFlagOff()
  {
    var stationOrder = BuildStationOrder(DeliveryMode.Together);

    ErrorOr<StationOrder> outcome = _service.HideFromAsItComesQueue(stationOrder);

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

    ErrorOr<StationOrder> outcome = _service.HideFromAsItComesQueue(stationOrder);

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

    ErrorOr<StationOrder> outcome = _service.HideFromAsItComesQueue(stationOrder);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(stationOrder.IsHiddenFromAsItComesQueue, Is.True);
                    });
  }

  [Test]
  public void IsInAsItComesColumn_AnAsItComesStationOrderNobodyHid_IsTrue()
  {
    Assert.That(_service.IsInAsItComesColumn(BuildStationOrder(DeliveryMode.AsItComes)), Is.True);
  }

  [Test]
  public void IsInAsItComesColumn_AnAsItComesStationOrderTheEmployeeHid_IsFalse()
  {
    var stationOrder = BuildStationOrder(DeliveryMode.AsItComes);
    stationOrder.IsHiddenFromAsItComesQueue = true;

    Assert.That(_service.IsInAsItComesColumn(stationOrder), Is.False);
  }

  [Test]
  public void IsInAsItComesColumn_ATogetherStationOrder_IsFalse()
  {
    Assert.That(_service.IsInAsItComesColumn(BuildStationOrder(DeliveryMode.Together)), Is.False);
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
