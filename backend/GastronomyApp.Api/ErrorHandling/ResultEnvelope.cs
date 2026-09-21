using ErrorOr;
using GastronomyApp.Contracts;
using GastronomyApp.Core.Refusals;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.ErrorHandling;

public sealed class ResultEnvelope
{
  private readonly IHttpContextAccessor _httpContextAccessor;
  private readonly ILogger<ResultEnvelope> _logger;
  private readonly SystemTextJsonRecordingSerializer _recordingSerializer;

  public ResultEnvelope(SystemTextJsonRecordingSerializer recordingSerializer, IHttpContextAccessor httpContextAccessor, ILogger<ResultEnvelope> logger)
  {
    _recordingSerializer = recordingSerializer;
    _httpContextAccessor = httpContextAccessor;
    _logger = logger;
  }

  public IResult Refuse(List<Error> refusedLines)
  {
    ArgumentNullException.ThrowIfNull(refusedLines);

    LogTheRefusedRequest(refusedLines);

    var answered = refusedLines[0];
    var statusCode = answered.NumericType;

    if (ProblemCodeOf(answered) is not { } problemCode)
      return Results.StatusCode(statusCode);

    return ToResult(new()
                    {
                      StatusCode = statusCode,
                      Error = new()
                              {
                                Code = problemCode,
                                MessageKey = answered.Code,
                                Parameters = ParametersOf(answered)
                              }
                    });
  }

  public IResult ToResult(ProblemDescription problem)
  {
    ArgumentNullException.ThrowIfNull(problem);

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

  private void LogTheRefusedRequest(List<Error> refusedLines)
  {
    IErrorOr refusal = ErrorOrFactory.From<Success>(refusedLines);

    _logger.LogWarning("The request to {RequestPath} was refused: {Refusal}", _httpContextAccessor.HttpContext?.Request.Path.Value, refusal.GetRecording(_recordingSerializer));
  }

  private string? ProblemCodeOf(Error refusal)
  {
    if (refusal.Metadata is { } metadata && metadata.TryGetValue(Refusal.MetadataKeys.ProblemCode, out var problemCode))
      return problemCode.ToString();

    return null;
  }

  private IReadOnlyDictionary<string, string> ParametersOf(Error refusal)
  {
    if (refusal.Metadata is not { } metadata)
      return new Dictionary<string, string>();

    return metadata.Where(entry => entry.Key != Refusal.MetadataKeys.ProblemCode).ToDictionary(entry => entry.Key, entry => entry.Value.ToString() ?? string.Empty, StringComparer.Ordinal);
  }
}
