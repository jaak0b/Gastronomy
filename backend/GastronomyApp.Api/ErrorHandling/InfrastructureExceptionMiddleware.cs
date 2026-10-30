using GastronomyApp.Core.Refusals;
using GastronomyApp.Infrastructure.Enums;
using GastronomyApp.Infrastructure.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.ErrorHandling;

public sealed class InfrastructureExceptionMiddleware : IMiddleware
{
  private readonly ILogger<InfrastructureExceptionMiddleware> _logger;
  private readonly ResultEnvelope _resultEnvelope;

  public InfrastructureExceptionMiddleware(ResultEnvelope resultEnvelope, ILogger<InfrastructureExceptionMiddleware> logger)
  {
    _resultEnvelope = resultEnvelope;
    _logger = logger;
  }

  public async Task InvokeAsync(HttpContext context, RequestDelegate next)
  {
    try
    {
      await next(context);
    }
    catch (InfrastructureException exception) when (exception.Reason == InfrastructureFailureReason.DatabaseUnavailable)
    {
      _logger.LogError(exception, "The request to {RequestPath} could not use the database.", context.Request.Path.Value);

      await _resultEnvelope.Refuse([Refusal.Storage.DatabaseUnavailable()]).ExecuteAsync(context);
    }
    catch (InfrastructureException exception) when (exception.Reason == InfrastructureFailureReason.ConflictingChange)
    {
      await _resultEnvelope.Refuse([Refusal.Storage.ConflictingChange()]).ExecuteAsync(context);
    }
  }
}
