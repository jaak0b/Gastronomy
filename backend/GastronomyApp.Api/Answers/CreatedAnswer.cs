using System.Reflection;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;

namespace GastronomyApp.Api.Answers;

public sealed class CreatedAnswer<TView> : ViewOrRefusal<TView>, IEndpointMetadataProvider
{
  public CreatedAnswer(ErrorOr<TView> outcome) : base(outcome)
  {
  }

  public static implicit operator CreatedAnswer<TView>(ErrorOr<TView> outcome)
  {
    return new(outcome);
  }

  public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status201Created, typeof(TView), [ApplicationJson]));
  }

  protected override IResult SuccessAnswerFor(TView view)
  {
    return Results.Json(view, statusCode: StatusCodes.Status201Created);
  }
}
