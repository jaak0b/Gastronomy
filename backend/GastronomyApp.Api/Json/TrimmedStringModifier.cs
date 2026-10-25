using System.Text.Json.Serialization.Metadata;

namespace GastronomyApp.Api.Json;

public sealed class TrimmedStringModifier
{
  public void Trim(JsonTypeInfo typeInfo)
  {
    ArgumentNullException.ThrowIfNull(typeInfo);

    foreach (var property in typeInfo.Properties)
    {
      if (property.PropertyType != typeof(string) || property.Set is not { } setProperty)
        continue;

      property.Set = (owner, value) => setProperty(owner, (value as string)?.Trim());
    }
  }
}
