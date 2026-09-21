using GastronomyApp.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.ErrorHandling;

public sealed class RequestShapeRefusalWriter : IProblemDetailsService
{
  private const string ValidationFailedCode = Names.ProblemCodes.ValidationFailed;

  private readonly ILogger<RequestShapeRefusalWriter> _log;

  public RequestShapeRefusalWriter(ILogger<RequestShapeRefusalWriter> log)
  {
    _log = log;
  }

  public async ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
  {
    ArgumentNullException.ThrowIfNull(context);

    if (context.ProblemDetails is not HttpValidationProblemDetails refusedShape)
      return false;

    var messageKey = FirstMessageKey(refusedShape);

    if (messageKey is null)
    {
      _log.LogError("A request to {Path} was refused by the contract, but the refusal named no message key, so the phone cannot be told what went wrong.", context.HttpContext.Request.Path);

      return false;
    }

    _log.LogWarning("A request to {Path} was refused because the body does not match the contract: {RefusedMembers}.", context.HttpContext.Request.Path, RenderedMembers(refusedShape));

    context.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

    await context.HttpContext.Response.WriteAsJsonAsync(new ApiError
                                                        {
                                                          Code = ValidationFailedCode,
                                                          MessageKey = messageKey,
                                                          Parameters = new Dictionary<string, string>()
                                                        });

    return true;
  }

  public async ValueTask WriteAsync(ProblemDetailsContext context)
  {
    if (!await TryWriteAsync(context))
      throw new InvalidOperationException("A refusal was handed to the request shape refusal writer that it cannot describe to the caller.");
  }

  private string? FirstMessageKey(HttpValidationProblemDetails refusedShape)
  {
    return refusedShape.Errors.SelectMany(refusedMember => refusedMember.Value).FirstOrDefault();
  }

  private string RenderedMembers(HttpValidationProblemDetails refusedShape)
  {
    return string.Join("; ", refusedShape.Errors.Select(refusedMember => $"{refusedMember.Key}: {string.Join(", ", refusedMember.Value)}"));
  }
}
