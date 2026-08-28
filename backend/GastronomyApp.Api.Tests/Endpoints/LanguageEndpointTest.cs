using System.Net;
using System.Text.Json;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class LanguageEndpointTest
{

  [SetUp]
  public async Task SetUp()
  {
    context = await new OrderTestContext.Builder().StartAsync(false);
  }

  [TearDown]
  public async Task TearDown()
  {
    await context.DisposeAsync();
  }

  private OrderTestContext context = null!;

  [Test]
  public async Task GetLanguage_FreshLaptop_ReportsTheLanguageTheOperatorChose()
  {
    using var response = await context.Client.GetAsync("/api/language");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("language").GetString(), Is.EqualTo("de"));
                    });
  }

  [Test]
  public async Task GetLanguage_OperatorSwitchedTheLaptop_ReportsTheNewLanguageWithoutARestart()
  {
    context.Factory.Language.Current = "en";

    using var response = await context.Client.GetAsync("/api/language");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.That(body.RootElement.GetProperty("language").GetString(), Is.EqualTo("en"));
  }
}
