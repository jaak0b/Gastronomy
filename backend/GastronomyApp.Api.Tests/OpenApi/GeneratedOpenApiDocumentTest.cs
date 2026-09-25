using System.Text.Json;

namespace GastronomyApp.Api.Tests.OpenApi;

[TestFixture]
public sealed class GeneratedOpenApiDocumentTest
{
  private JsonElement _document;

  [OneTimeSetUp]
  public void ReadTheGeneratedDocument()
  {
    var documentPath = Path.Combine(AppContext.BaseDirectory, "openapi", "GastronomyApp.Api.json");
    Assert.That(File.Exists(documentPath), Is.True, $"The generated OpenAPI document was not found at {documentPath}.");
    _document = JsonDocument.Parse(File.ReadAllText(documentPath)).RootElement.Clone();
  }

  [Test]
  public void Paths_ForTheStationQueueRoute_DescribeTheStationQueueView()
  {
    var response = _document.GetProperty("paths")
                            .GetProperty("/api/station/orders")
                            .GetProperty("get")
                            .GetProperty("responses")
                            .GetProperty("200")
                            .GetProperty("content")
                            .GetProperty("application/json")
                            .GetProperty("schema")
                            .GetProperty("$ref")
                            .GetString();

    Assert.That(response, Is.EqualTo("#/components/schemas/StationQueueView"));
  }

  [Test]
  public void Schemas_ForAHubEventNoEndpointReturns_AreGenerated()
  {
    var schemas = _document.GetProperty("components").GetProperty("schemas");

    Assert.That(schemas.TryGetProperty("DeviceRevokedEvent", out var deviceRevoked), Is.True);
    Assert.That(deviceRevoked.GetProperty("properties").TryGetProperty("deviceId", out _), Is.True);
  }

  [Test]
  public void Schemas_ForDeliveryMode_KeepTheCamelCaseTextWireFormat()
  {
    var values = _document.GetProperty("components")
                          .GetProperty("schemas")
                          .GetProperty("DeliveryMode")
                          .GetProperty("enum")
                          .EnumerateArray()
                          .Select(value => value.GetString())
                          .ToList();

    Assert.That(values, Is.EqualTo(new[] { "together", "asItComes" }));
  }

  [Test]
  public void Schemas_ForAnInteger_DescribeItAsANumberAndNeverAsText()
  {
    var sortOrder = _document.GetProperty("components")
                             .GetProperty("schemas")
                             .GetProperty("AdminStationView")
                             .GetProperty("properties")
                             .GetProperty("sortOrder");

    Assert.That(sortOrder.GetProperty("type").GetString(), Is.EqualTo("integer"));
  }
}
