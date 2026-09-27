using System.Text.Json;
using GastronomyApp.Contracts;

namespace GastronomyApp.Api.Tests.Contracts;

[TestFixture]
public sealed class ApiErrorTest
{
  [Test]
  public void Serialize_PopulatedError_ProducesTheSpecifiedEnvelopeShape()
  {
    ApiError error = new()
                     {
                       Code = "StationHasUnfinishedItems",
                       MessageKey = "errors.admin.stations.hasUnfinishedItems",
                       Parameters = new Dictionary<string, string> { ["station"] = "Küche" },
                       Details = null
                     };

    var json = JsonSerializer.Serialize(error, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    Assert.That(json, Is.EqualTo("{\"code\":\"StationHasUnfinishedItems\",\"messageKey\":\"errors.admin.stations.hasUnfinishedItems\",\"parameters\":{\"station\":\"K\\u00FCche\"},\"details\":null}"));
  }
}
