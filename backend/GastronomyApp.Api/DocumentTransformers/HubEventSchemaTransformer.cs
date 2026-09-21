using GastronomyApp.Contracts.Events;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace GastronomyApp.Api.DocumentTransformers;

public sealed class HubEventSchemaTransformer : IOpenApiDocumentTransformer
{
  public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(document);
    ArgumentNullException.ThrowIfNull(context);

    document.Components ??= new();
    document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

    foreach (var eventType in HubEventTypes())
    {
      var schema = await context.GetOrCreateSchemaAsync(eventType, null, cancellationToken);
      document.Components.Schemas[eventType.Name] = schema;
    }
  }

  private IEnumerable<Type> HubEventTypes()
  {
    var eventsNamespace = typeof(CatalogChangedEvent).Namespace;
    return typeof(CatalogChangedEvent).Assembly.GetTypes().Where(type => type.IsClass && type.IsPublic && type.Namespace == eventsNamespace).OrderBy(type => type.Name, StringComparer.Ordinal);
  }
}
