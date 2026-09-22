using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Answers;

public abstract class ViewOrRefusal<TView> : IResult, IErrorOr
{
  protected const string ApplicationJson = "application/json";

  private readonly ErrorOr<TView> _outcome;
  private readonly IErrorOr _typelessOutcome;

  protected ViewOrRefusal(ErrorOr<TView> outcome)
  {
    _outcome = outcome;
    _typelessOutcome = outcome;
  }

  List<Error>? IErrorOr.Errors
  {
    get
    {
      return _typelessOutcome.Errors;
    }
  }

  bool IErrorOr.IsError
  {
    get
    {
      return _typelessOutcome.IsError;
    }
  }

  bool IErrorOr.IsSuccess
  {
    get
    {
      return _typelessOutcome.IsSuccess;
    }
  }

  IEnumerator<Error> IErrorOr.GetEnumerator()
  {
    return _typelessOutcome.GetEnumerator();
  }

  TOutput IRecordable.GetRecording<TOutput>(IRecordingSerializer<TOutput> serializer)
  {
    return _typelessOutcome.GetRecording(serializer);
  }

  public Task ExecuteAsync(HttpContext httpContext)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    if (_outcome.IsError)
      return httpContext.RequestServices.GetRequiredService<ResultEnvelope>().Refuse(_outcome.Errors).ExecuteAsync(httpContext);

    return SuccessAnswerFor(_outcome.Value).ExecuteAsync(httpContext);
  }

  protected abstract IResult SuccessAnswerFor(TView view);
}
