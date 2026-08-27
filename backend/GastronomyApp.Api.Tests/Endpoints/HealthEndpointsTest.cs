using System.Net;
using System.Text.Json;
using GastronomyApp.Infrastructure;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class HealthEndpointsTest
{
    private ApiTestFactory factory = null!;

    [SetUp]
    public async Task SetUp()
    {
        factory = await new ApiTestFactory.Builder().StartAsync();
        await using GastronomyAppDbContext context = factory.CreateContext();
        await new ApiSeeder().SeedAsync(context, CancellationToken.None);
    }

    [TearDown]
    public async Task TearDown()
    {
        await factory.DisposeAsync();
    }

    [Test]
    public async Task GetHealth_AnonymousCaller_ReportsThePrinterCounts()
    {
        using HttpResponseMessage response = await factory.Client.GetAsync("/api/health");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.RootElement.GetProperty("status").GetString(), Is.EqualTo("ok"));
            Assert.That(body.RootElement.GetProperty("printersOnline").GetInt32(), Is.EqualTo(2));
            Assert.That(body.RootElement.GetProperty("printersTotal").GetInt32(), Is.EqualTo(2));
        });
    }
}
