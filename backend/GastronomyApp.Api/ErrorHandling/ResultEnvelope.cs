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

  public ProblemDescription Describe(OrderValidationFailure failure)
  {
    return failure.Reason switch
           {
             OrderValidationFailureReason.NoItems =>
               Validation(CannotBeProcessedKey),
             OrderValidationFailureReason.TableNameMissing =>
               Validation(CannotBeProcessedKey),
             OrderValidationFailureReason.PriceOutOfRange =>
               Validation(CannotBeProcessedKey),
             OrderValidationFailureReason.StationRequired =>
               Unprocessable(CannotBeProcessedKey, null),
             OrderValidationFailureReason.ItemHasNoStation =>
               Unprocessable(CannotBeProcessedKey, null),
             OrderValidationFailureReason.NoRunningFestival =>
               Unprocessable(CannotBeProcessedKey, null),
             OrderValidationFailureReason.OrderNumberCouldNotBeAllocated =>
               Unprocessable(CannotBeProcessedKey, null),
             OrderValidationFailureReason.UnknownCatalogItemId =>
               Unprocessable("order.unknownItem", failure.OffendingCatalogItemId),
             OrderValidationFailureReason.StationNotAssignedToItem =>
               Unprocessable("order.stationNotAssignedToItem", failure.OffendingCatalogItemId),
             _ => new Never().OfType<ProblemDescription>(failure.Reason)
           };
  }

  public ProblemDescription Describe(SettlementFailure failure)
  {
    return failure.Reason switch
           {
             SettlementFailureReason.NoItemsSelected =>
               Validation("order.settlementNoItemsSelected"),
             SettlementFailureReason.TooManyItemsSelected =>
               Validation("order.settlementTooManyItemsSelected"),
             SettlementFailureReason.PaymentNoticeMissing =>
               Validation(SettlementCannotBeProcessedKey),
             SettlementFailureReason.UnknownOrderItemId =>
               Unprocessable("order.settlementUnknownItem", "orderItemId", failure.OffendingOrderItemId),
             SettlementFailureReason.AmountPaidMissing =>
               Validation(SettlementCannotBeProcessedKey),
             SettlementFailureReason.AmountPaidNegative =>
               Validation(SettlementCannotBeProcessedKey),
             SettlementFailureReason.SelectionSpansSeveralTables =>
               Validation(SettlementCannotBeProcessedKey),
             SettlementFailureReason.NoRunningFestival =>
               Validation(SettlementCannotBeProcessedKey),
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
    return Unprocessable(messageKey, "catalogItemId", offendingCatalogItemId);
  }

  private ProblemDescription Unprocessable(string messageKey, string parameterName, Guid? offendingId)
  {
    Dictionary<string, string> parameters = [];
    if (offendingId is not null)
    {
      parameters[parameterName] = offendingId.Value.ToString();
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
