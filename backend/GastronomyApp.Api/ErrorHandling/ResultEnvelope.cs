using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.ErrorHandling;

public sealed class ResultEnvelope
{
  private const string ValidationFailedCode = "ValidationFailed";
  private const string UnprocessableEntityCode = "UnprocessableEntity";
  private const string CannotBeProcessedKey = "order.cannotBeProcessed";
  private const string SettlementCannotBeProcessedKey = "order.settlementCannotBeProcessed";

  public ProblemDescription BuildProblemDescription(OrderValidationFailure failure)
  {
    return failure.Reason switch
           {
             OrderValidationFailureReason.NoItems =>
               BuildValidationProblem(CannotBeProcessedKey),
             OrderValidationFailureReason.TableNameMissing =>
               BuildValidationProblem(CannotBeProcessedKey),
             OrderValidationFailureReason.PriceOutOfRange =>
               BuildValidationProblem(CannotBeProcessedKey),
             OrderValidationFailureReason.StationRequired =>
               BuildUnprocessableProblem(CannotBeProcessedKey, null),
             OrderValidationFailureReason.ItemHasNoStation =>
               BuildUnprocessableProblem(CannotBeProcessedKey, null),
             OrderValidationFailureReason.NoRunningFestival =>
               BuildUnprocessableProblem(CannotBeProcessedKey, null),
             OrderValidationFailureReason.OrderNumberCouldNotBeAllocated =>
               BuildUnprocessableProblem(CannotBeProcessedKey, null),
             OrderValidationFailureReason.SettlementCannotBeProcessed =>
               BuildValidationProblem(SettlementCannotBeProcessedKey),
             OrderValidationFailureReason.UnknownCatalogItemId =>
               BuildUnprocessableProblem("order.unknownItem", failure.OffendingCatalogItemId),
             OrderValidationFailureReason.StationNotAssignedToItem =>
               BuildUnprocessableProblem("order.stationNotAssignedToItem", failure.OffendingCatalogItemId),
             OrderValidationFailureReason.ItemNotAvailable =>
               BuildUnprocessableProblemWithParameters("catalog.itemSoldOut", SoldOutParameters(failure)),
             _ => new Never().OfType<ProblemDescription>(failure.Reason)
           };
  }

  public ProblemDescription BuildProblemDescription(SettlementFailure failure)
  {
    return failure.Reason switch
           {
             SettlementFailureReason.NoItemsSelected =>
               BuildValidationProblem("order.settlementNoItemsSelected"),
             SettlementFailureReason.PaymentNoticeMissing =>
               BuildValidationProblem(SettlementCannotBeProcessedKey),
             SettlementFailureReason.UnknownOrderItemId =>
               BuildUnprocessableProblem("order.settlementUnknownItem", "orderItemId", failure.OffendingOrderItemId),
             SettlementFailureReason.AmountPaidMissing =>
               BuildValidationProblem(SettlementCannotBeProcessedKey),
             SettlementFailureReason.AmountPaidNegative =>
               BuildValidationProblem(SettlementCannotBeProcessedKey),
             SettlementFailureReason.DuplicateOrderItemId =>
               BuildValidationProblem(SettlementCannotBeProcessedKey),
             SettlementFailureReason.SelectionSpansSeveralTables =>
               BuildValidationProblem(SettlementCannotBeProcessedKey),
             SettlementFailureReason.NoRunningFestival =>
               BuildValidationProblem(SettlementCannotBeProcessedKey),
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

  private ProblemDescription BuildValidationProblem(string messageKey)
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

  private ProblemDescription BuildUnprocessableProblem(string messageKey, Guid? offendingCatalogItemId)
  {
    return BuildUnprocessableProblem(messageKey, "catalogItemId", offendingCatalogItemId);
  }

  private ProblemDescription BuildUnprocessableProblem(string messageKey, string parameterName, Guid? offendingId)
  {
    Dictionary<string, string> parameters = [];
    if (offendingId is not null)
    {
      parameters[parameterName] = offendingId.Value.ToString();
    }

    return BuildUnprocessableProblemWithParameters(messageKey, parameters);
  }

  private ProblemDescription BuildUnprocessableProblemWithParameters(string messageKey, IReadOnlyDictionary<string, string> parameters)
  {
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

  private Dictionary<string, string> SoldOutParameters(OrderValidationFailure failure)
  {
    Dictionary<string, string> parameters = [];
    if (failure.OffendingCatalogItemId is { } catalogItemId)
    {
      parameters["catalogItemId"] = catalogItemId.ToString();
    }
    if (failure.OffendingCatalogItemName is { } itemName)
    {
      parameters["name"] = itemName;
    }

    return parameters;
  }
}
