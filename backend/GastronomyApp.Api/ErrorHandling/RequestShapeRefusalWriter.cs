using ErrorOr;
using GastronomyApp.Core.Refusals;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;

namespace GastronomyApp.Api.ErrorHandling;

public sealed class RequestShapeRefusalWriter : IProblemDetailsService
{
  private readonly ResultEnvelope _envelope;

  public RequestShapeRefusalWriter(ResultEnvelope envelope)
  {
    _envelope = envelope;
  }

  public async ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
  {
    ArgumentNullException.ThrowIfNull(context);

    if (context.ProblemDetails is not HttpValidationProblemDetails refusedShape)
      return false;

    var refusedMembers = CollectRefusals(refusedShape, FindRequestType(context.HttpContext));

    if (refusedMembers.Count == 0)
      throw new InvalidOperationException($"A request to {context.HttpContext.Request.Path} was refused by the contract, but the refusal named no member, so there is nothing the phone could be told.");

    await _envelope.Refuse(refusedMembers).ExecuteAsync(context.HttpContext);

    return true;
  }

  public async ValueTask WriteAsync(ProblemDetailsContext context)
  {
    if (!await TryWriteAsync(context))
      throw new InvalidOperationException("A refusal was handed to the request shape refusal writer that it cannot describe to the caller.");
  }

  private List<Error> CollectRefusals(HttpValidationProblemDetails refusedShape, Type? requestType)
  {
    IReadOnlyList<string> declaredMembers = ReadDeclaredMemberNames(requestType);

    return refusedShape.Errors
                       .OrderBy(refusedMember => PositionOfMember(declaredMembers, refusedMember.Key))
                       .ThenBy(refusedMember => refusedMember.Key, StringComparer.Ordinal)
                       .SelectMany(refusedMember => refusedMember.Value.Select(messageKey => Refusal.RequestShape.MemberRefused(refusedMember.Key, messageKey)))
                       .ToList();
  }

  private Type? FindRequestType(HttpContext httpContext)
  {
    return httpContext.GetEndpoint()?.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType;
  }

  private IReadOnlyList<string> ReadDeclaredMemberNames(Type? requestType)
  {
    if (requestType is null)
      return [];

    return requestType.GetProperties().Select(property => property.Name).ToList();
  }

  private int PositionOfMember(IReadOnlyList<string> declaredMembers, string member)
  {
    var rootMember = member.Split('.', '[')[0];

    for (var position = 0; position < declaredMembers.Count; position++)
      if (string.Equals(declaredMembers[position], rootMember, StringComparison.OrdinalIgnoreCase))
        return position;

    return int.MaxValue;
  }
}
