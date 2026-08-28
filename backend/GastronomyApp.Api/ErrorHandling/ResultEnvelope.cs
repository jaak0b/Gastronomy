using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.ErrorHandling;

public sealed class ResultEnvelope
{
  private const string ValidationFailedCode = "ValidationFailed";
  private const string UnprocessableEntityCode = "UnprocessableEntity";

  public ProblemDescription Describe(OrderValidationFailure failure)
  {
    return failure.Reason switch
           {
             OrderValidationFailureReason.NoItems =>
               Validation("order.noItems"),
             OrderValidationFailureReason.TooManyItems =>
               Validation("order.tooManyItems"),
             OrderValidationFailureReason.TableNameMissing =>
               Validation("order.tableNameMissing"),
             OrderValidationFailureReason.TableNameTooLong =>
               Validation("order.tableNameTooLong"),
             OrderValidationFailureReason.UnknownCatalogItemId =>
               Unprocessable("order.unknownItem", failure.OffendingCatalogItemId),
             OrderValidationFailureReason.StationRequired =>
               Unprocessable("order.stationRequired", failure.OffendingCatalogItemId),
             OrderValidationFailureReason.StationNotAssignedToItem =>
               Unprocessable("order.stationNotAssignedToItem", failure.OffendingCatalogItemId),
             OrderValidationFailureReason.PriceOutOfRange =>
               Validation("order.priceOutOfRange"),
             OrderValidationFailureReason.ItemHasNoStation =>
               Unprocessable("order.itemHasNoStation", failure.OffendingCatalogItemId),
             _ => new Never().OfType<ProblemDescription>(failure.Reason)
           };
  }

  public ProblemDescription Describe(RoutingFailure failure)
  {
    return failure.Reason switch
           {
             RoutingFailureReason.ItemHasNoStation => Unprocessable("order.itemHasNoStation", null),
             RoutingFailureReason.StationRequired => Unprocessable("order.stationRequired", null),
             RoutingFailureReason.StationNotAssignedToItem => Unprocessable("order.stationNotAssignedToItem", null),
             _ => new Never().OfType<ProblemDescription>(failure.Reason)
           };
  }

  public IResult ToResult(ProblemDescription problem)
  {
    return Results.Json(problem.Error, statusCode: problem.StatusCode);
  }

  public IResult Problem(int statusCode, string code, string messageKey, IReadOnlyDictionary<string, string> parameters)
  {
    return Results.Json(new ApiError
                        {
                          Code = code,
                          MessageKey = messageKey,
                          Parameters = parameters
                        },
                        statusCode: statusCode);
  }

  public IResult Problem(int statusCode, string code, string messageKey)
  {
    return Problem(statusCode, code, messageKey, new Dictionary<string, string>());
  }

  private ProblemDescription Validation(string messageKey)
  {
    return new()
           {
             StatusCode = StatusCodes.Status400BadRequest,
             Error = new()
                     {
                       Code = ValidationFailedCode,
                       MessageKey = messageKey,
                       Parameters = new Dictionary<string, string>()
                     }
           };
  }

  private ProblemDescription Unprocessable(string messageKey, Guid? offendingCatalogItemId)
  {
    Dictionary<string, string> parameters = [];
    if (offendingCatalogItemId is not null)
    {
      parameters["catalogItemId"] = offendingCatalogItemId.Value.ToString();
    }

    return new()
           {
             StatusCode = StatusCodes.Status422UnprocessableEntity,
             Error = new()
                     {
                       Code = UnprocessableEntityCode,
                       MessageKey = messageKey,
                       Parameters = parameters
                     }
           };
  }
}
