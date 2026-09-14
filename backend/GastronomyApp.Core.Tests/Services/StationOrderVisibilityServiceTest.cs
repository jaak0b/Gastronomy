using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

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
  public void HideFromAsItComesQueue_ATogetherSlice_IsRefusedAndLeavesTheFlagOff()
  {
    var slice = SliceWith(DeliveryMode.Together);

    Result<StationOrder, StationOrderVisibilityFailure> outcome = _service.HideFromAsItComesQueue(slice);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.False);
                      Assert.That(outcome.Failure.Reason,
                                  Is.EqualTo(StationOrderVisibilityFailureReason.NotAnAsItComesOrder));
                      Assert.That(slice.IsHiddenFromAsItComesQueue, Is.False);
                    });
  }

  [Test]
  public void HideFromAsItComesQueue_AnAsItComesSlice_SetsTheFlag()
  {
    var slice = SliceWith(DeliveryMode.AsItComes);

    Result<StationOrder, StationOrderVisibilityFailure> outcome = _service.HideFromAsItComesQueue(slice);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(outcome.Value, Is.SameAs(slice));
                      Assert.That(slice.IsHiddenFromAsItComesQueue, Is.True);
                    });
  }

  [Test]
  public void HideFromAsItComesQueue_AnAlreadyHiddenSlice_IsAccepted()
  {
    var slice = SliceWith(DeliveryMode.AsItComes);
    slice.IsHiddenFromAsItComesQueue = true;

    Result<StationOrder, StationOrderVisibilityFailure> outcome = _service.HideFromAsItComesQueue(slice);

    Assert.Multiple(() =>
                    {
                      Assert.That(outcome.IsSuccess, Is.True);
                      Assert.That(slice.IsHiddenFromAsItComesQueue, Is.True);
                    });
  }

  private StationOrder SliceWith(DeliveryMode deliveryMode)
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
