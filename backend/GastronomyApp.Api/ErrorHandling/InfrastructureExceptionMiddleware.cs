using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.ErrorHandling;

public sealed class InfrastructureExceptionMiddleware : IMiddleware
{
  private readonly ResultEnvelope resultEnvelope;

  public InfrastructureExceptionMiddleware(ResultEnvelope resultEnvelope)
  {
    this.resultEnvelope = resultEnvelope;
  }

  public async Task InvokeAsync(HttpContext context, RequestDelegate next)
  {
    try
    {
      await next(context);
    }
    catch (InfrastructureException exception)
        when (exception.Reason == InfrastructureFailureReason.DatabaseUnavailable)
    {
      IResult problem = resultEnvelope.Problem(
          StatusCodes.Status503ServiceUnavailable,
          "DatabaseUnavailable",
          "review.sendFailedDatabase");

      await problem.ExecuteAsync(context);
    }
    catch (InfrastructureException exception)
        when (exception.Reason == InfrastructureFailureReason.ConflictingChange)
    {
      IResult problem = resultEnvelope.Problem(
          StatusCodes.Status409Conflict,
          "ConflictingChange",
          "review.conflictingChange");

      await problem.ExecuteAsync(context);
    }
  }
}
