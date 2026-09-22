using GastronomyApp.Core.Refusals;
using GastronomyApp.Infrastructure.Enums;
using GastronomyApp.Infrastructure.ErrorHandling;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.ErrorHandling;

public sealed class InfrastructureExceptionMiddleware : IMiddleware
{
  private readonly ResultEnvelope _resultEnvelope;

  public InfrastructureExceptionMiddleware(ResultEnvelope resultEnvelope)
  {
    _resultEnvelope = resultEnvelope;
  }

  public async Task InvokeAsync(HttpContext context, RequestDelegate next)
  {
    try
    {
      await next(context);
    }
    catch (InfrastructureException exception) when (exception.Reason == InfrastructureFailureReason.DatabaseUnavailable)
    {
      await _resultEnvelope.Refuse([Refusal.Storage.DatabaseUnavailable()]).ExecuteAsync(context);
    }
    catch (InfrastructureException exception) when (exception.Reason == InfrastructureFailureReason.ConflictingChange)
    {
      await _resultEnvelope.Refuse([Refusal.Storage.ConflictingChange()]).ExecuteAsync(context);
    }
  }
}
