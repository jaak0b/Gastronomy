using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Responders;

public sealed class StationQueueRefusalResponder
{
  private readonly ResultEnvelope _resultEnvelope;

  public StationQueueRefusalResponder(ResultEnvelope resultEnvelope)
  {
    _resultEnvelope = resultEnvelope;
  }

  public IResult Respond(StationQueueFailure failure)
  {
    ArgumentNullException.ThrowIfNull(failure);

    return failure.Reason switch
           {
             StationQueueFailureReason.StationUnknown => Results.Unauthorized(),
             StationQueueFailureReason.NoRunningFestival => _resultEnvelope.Problem(StatusCodes.Status409Conflict, "NoRunningFestival", "station.noFestivalIsRunning"),
             StationQueueFailureReason.StationNotAtTheFestival => _resultEnvelope.Problem(StatusCodes.Status409Conflict, "StationNotAtTheFestival", "station.notPartOfTheFestival"),
             StationQueueFailureReason.UnknownOrderItemId => _resultEnvelope.Problem(StatusCodes.Status422UnprocessableEntity, "UnprocessableEntity", "station.itemNotAtThisStation"),
             StationQueueFailureReason.ItemNotFulfilled => _resultEnvelope.Problem(StatusCodes.Status409Conflict, "ItemNotFulfilled", "station.changeNotSaved"),
             StationQueueFailureReason.OrderNotAtThisStation => _resultEnvelope.Problem(StatusCodes.Status422UnprocessableEntity, "UnprocessableEntity", "station.orderNotAtThisStation"),
             StationQueueFailureReason.NotAnAsItComesOrder => _resultEnvelope.Problem(StatusCodes.Status409Conflict, "CannotHideTogetherOrder", "station.changeNotSaved"),
             _ => new UnreachableCase().Throw<IResult>(failure.Reason)
           };
  }
}
