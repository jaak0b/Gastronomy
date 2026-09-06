using System.Text.Json;
using GastronomyApp.Api.ErrorHandling;

namespace GastronomyApp.Api.Tests.ErrorHandling;

[TestFixture]
public sealed class ApiErrorTest
{
  [Test]
  public void Serialize_PopulatedError_ProducesTheSpecifiedEnvelopeShape()
  {
    ApiError error = new()
                     {
                       Code = "StationHasUnfinishedItems",
                       MessageKey = "admin.stationHasUnfinishedItems",
                       Parameters = new Dictionary<string, string> { ["station"] = "Küche" },
                       Details = null
                     };

    var json = JsonSerializer.Serialize(error,
                                        new JsonSerializerOptions
                                        {
                                          PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                                        });

    Assert.That(json,
                Is.EqualTo("{\"code\":\"StationHasUnfinishedItems\",\"messageKey\":\"admin.stationHasUnfinishedItems\",\"parameters\":{\"station\":\"K\\u00FCche\"},\"details\":null}"));
  }
}
