using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace GastronomyApp.Api.DocumentTransformers;

public sealed class StringEncodedNumberSchemaTransformer : IOpenApiSchemaTransformer
{
  public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(schema);

    if (schema.Type is not { } type)
      return Task.CompletedTask;

    var isNumeric = type.HasFlag(JsonSchemaType.Integer) || type.HasFlag(JsonSchemaType.Number);
    if (!isNumeric || !type.HasFlag(JsonSchemaType.String))
      return Task.CompletedTask;

    schema.Type = type & ~JsonSchemaType.String;
    schema.Pattern = null;

    return Task.CompletedTask;
  }
}
