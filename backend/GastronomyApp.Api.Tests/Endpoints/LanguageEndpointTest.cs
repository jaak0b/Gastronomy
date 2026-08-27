using System.Net;
using System.Text.Json;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class LanguageEndpointTest
{
    private OrderTestContext context = null!;

    [SetUp]
    public async Task SetUp()
    {
        context = await new OrderTestContext.Builder().StartAsync(withRunningPrinters: false);
    }

    [TearDown]
    public async Task TearDown()
    {
        await context.DisposeAsync();
    }

    [Test]
    public async Task GetLanguage_FreshLaptop_ReportsTheLanguageTheOperatorChose()
    {
        using HttpResponseMessage response = await context.Client.GetAsync("/api/language");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

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

        using HttpResponseMessage response = await context.Client.GetAsync("/api/language");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(body.RootElement.GetProperty("language").GetString(), Is.EqualTo("en"));
    }
}
