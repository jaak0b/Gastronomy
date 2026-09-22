using System.Reflection;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;

namespace GastronomyApp.Api.Answers;

public sealed class NoContentAnswer : ViewOrRefusal<Success>, IEndpointMetadataProvider
{
  public NoContentAnswer(ErrorOr<Success> outcome) : base(outcome)
  {
  }

  public static implicit operator NoContentAnswer(ErrorOr<Success> outcome)
  {
    return new(outcome);
  }

  public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status204NoContent, typeof(void)));
  }

  protected override IResult SuccessAnswerFor(Success view)
  {
    return Results.NoContent();
  }
}
