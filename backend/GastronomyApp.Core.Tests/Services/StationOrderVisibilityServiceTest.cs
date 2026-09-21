using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class StationOrderVisibilityServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _service = new();
  }

  private StationOrderVisibilityService _service = null!;

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
