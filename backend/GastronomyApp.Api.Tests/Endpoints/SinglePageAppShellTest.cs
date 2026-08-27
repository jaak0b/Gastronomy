using System.Net;
using GastronomyApp.Infrastructure;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class SinglePageAppShellTest
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

    [TestCase("/")]
    [TestCase("/orders")]
    [TestCase("/admin")]
    public async Task Get_ClientRoutedPath_ServesTheSinglePageAppShell(string path)
    {
        using HttpResponseMessage response = await factory.Client.GetAsync(path);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("text/html"));
        });
    }

    [Test]
    public async Task Get_UnknownApiPath_IsNotAnsweredWithTheShell()
    {
        using HttpResponseMessage response = await factory.Client.GetAsync("/api/does-not-exist");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
