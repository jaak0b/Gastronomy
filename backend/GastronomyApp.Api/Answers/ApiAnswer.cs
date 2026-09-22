using System.Reflection;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;

namespace GastronomyApp.Api.Answers;

public sealed class ApiAnswer<TView> : ViewOrRefusal<TView>, IEndpointMetadataProvider
{
  public ApiAnswer(ErrorOr<TView> outcome) : base(outcome)
  {
  }

  public static implicit operator ApiAnswer<TView>(ErrorOr<TView> outcome)
  {
    return new(outcome);
  }

  public static implicit operator ApiAnswer<TView>(TView view)
  {
    return new(view);
  }

  public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(TView), [ApplicationJson]));
  }

  protected override IResult SuccessAnswerFor(TView view)
  {
    return Results.Json(view, statusCode: StatusCodes.Status200OK);
  }
}
