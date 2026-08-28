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
      Code = "PrinterOutOfPaper",
      MessageKey = "ticket.paperEnd",
      Parameters = new Dictionary<string, string> { ["station"] = "Küche" },
      Details = null,
    };

    string json = JsonSerializer.Serialize(error, new JsonSerializerOptions
    {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    });

    Assert.That(
        json,
        Is.EqualTo("{\"code\":\"PrinterOutOfPaper\",\"messageKey\":\"ticket.paperEnd\",\"parameters\":{\"station\":\"K\\u00FCche\"},\"details\":null}"));
  }
}
