using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Api.Tests.ErrorHandling;

[TestFixture]
public sealed class ResultEnvelopeTest
{
  private readonly ResultEnvelope envelope = new();

  [TestCase(OrderValidationFailureReason.NoItems, 400, "ValidationFailed")]
  [TestCase(OrderValidationFailureReason.TooManyItems, 400, "ValidationFailed")]
  [TestCase(OrderValidationFailureReason.TableNameMissing, 400, "ValidationFailed")]
  [TestCase(OrderValidationFailureReason.TableNameTooLong, 400, "ValidationFailed")]
  [TestCase(OrderValidationFailureReason.UnknownCatalogItemId, 422, "UnprocessableEntity")]
  [TestCase(OrderValidationFailureReason.StationRequired, 422, "UnprocessableEntity")]
  [TestCase(OrderValidationFailureReason.StationNotAssignedToItem, 422, "UnprocessableEntity")]
  public void Describe_OrderValidationFailure_MapsToItsStatusAndCode(OrderValidationFailureReason reason,
                                                                     int expectedStatusCode,
                                                                     string expectedCode)
  {
    var problem = envelope.Describe(new OrderValidationFailure { Reason = reason });

    Assert.Multiple(() =>
                    {
                      Assert.That(problem.StatusCode, Is.EqualTo(expectedStatusCode));
                      Assert.That(problem.Error.Code, Is.EqualTo(expectedCode));
                    });
  }

  [TestCase(RoutingFailureReason.StationRequired)]
  [TestCase(RoutingFailureReason.StationNotAssignedToItem)]
  public void Describe_RoutingFailure_MapsToUnprocessableEntity(RoutingFailureReason reason)
  {
    var problem = envelope.Describe(new RoutingFailure { Reason = reason });

    Assert.Multiple(() =>
                    {
                      Assert.That(problem.StatusCode, Is.EqualTo(422));
                      Assert.That(problem.Error.Code, Is.EqualTo("UnprocessableEntity"));
                    });
  }
}
