using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Api.Tests.ErrorHandling;

[TestFixture]
public sealed class ResultEnvelopeTest
{
  private readonly ResultEnvelope _envelope = new();

  [TestCase(OrderValidationFailureReason.NoItems, 400, "ValidationFailed", "order.cannotBeProcessed")]
  [TestCase(OrderValidationFailureReason.TableNameMissing, 400, "ValidationFailed", "order.cannotBeProcessed")]
  [TestCase(OrderValidationFailureReason.PriceOutOfRange, 400, "ValidationFailed", "order.cannotBeProcessed")]
  [TestCase(OrderValidationFailureReason.StationRequired, 422, "UnprocessableEntity", "order.cannotBeProcessed")]
  [TestCase(OrderValidationFailureReason.ItemHasNoStation, 422, "UnprocessableEntity", "order.cannotBeProcessed")]
  [TestCase(OrderValidationFailureReason.UnknownCatalogItemId, 422, "UnprocessableEntity", "order.unknownItem")]
  [TestCase(OrderValidationFailureReason.StationNotAssignedToItem, 422, "UnprocessableEntity", "order.stationNotAssignedToItem")]
  public void Describe_OrderValidationFailure_MapsToItsStatusCodeAndItsMessage(OrderValidationFailureReason reason,
                                                                              int expectedStatusCode,
                                                                              string expectedCode,
                                                                              string expectedMessageKey)
  {
    var problem = _envelope.Describe(new OrderValidationFailure { Reason = reason });

    Assert.Multiple(() =>
                    {
                      Assert.That(problem.StatusCode, Is.EqualTo(expectedStatusCode));
                      Assert.That(problem.Error.Code, Is.EqualTo(expectedCode));
                      Assert.That(problem.Error.MessageKey, Is.EqualTo(expectedMessageKey));
                    });
  }
}
