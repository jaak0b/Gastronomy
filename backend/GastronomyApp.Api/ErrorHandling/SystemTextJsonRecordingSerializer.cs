using System.Text.Json;
using System.Text.Json.Serialization;
using ErrorOr;

namespace GastronomyApp.Api.ErrorHandling;

public sealed class SystemTextJsonRecordingSerializer : IRecordingSerializer<string>
{
  private readonly JsonSerializerOptions _options = new()
                                                    {
                                                      WriteIndented = false,
                                                      Converters = { new JsonStringEnumConverter() },
                                                      DefaultIgnoreCondition = JsonIgnoreCondition.Never
                                                    };

  public string SerializeValue<TValue>(TValue value)
  {
    return JsonSerializer.Serialize(value, _options);
  }

  public string SerializeErrors(List<Error> errors)
  {
    return JsonSerializer.Serialize(errors, _options);
  }
}
