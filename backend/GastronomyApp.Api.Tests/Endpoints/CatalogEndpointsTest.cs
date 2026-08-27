using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class CatalogEndpointsTest
{
    private ApiTestFactory factory = null!;
    private SeededWorld world = null!;
    private string deviceToken = null!;

    [SetUp]
    public async Task SetUp()
    {
        factory = await new ApiTestFactory.Builder().StartAsync();
        await using GastronomyAppDbContext context = factory.CreateContext();
        world = await new ApiSeeder().SeedAsync(context, CancellationToken.None);

        using IServiceScope scope = factory.Services.CreateScope();
        IssuedDeviceToken issued = await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
            .IssueAsync(world.StaffMemberId, "de", "NUnit", CancellationToken.None);
        deviceToken = issued.PlaintextToken;
    }

    [TearDown]
    public async Task TearDown()
    {
        await factory.DisposeAsync();
    }

    [Test]
    public async Task GetCatalog_SeededCatalog_ReturnsTheShapeTheOrderingScreenNeeds()
    {
        JsonDocument body = await GetCatalogAsync();

        Assert.Multiple(() =>
        {
            Assert.That(body.RootElement.GetProperty("version").GetString(), Is.Not.Empty);
            Assert.That(body.RootElement.GetProperty("categories").GetArrayLength(), Is.EqualTo(2));
            Assert.That(body.RootElement.GetProperty("items").GetArrayLength(), Is.EqualTo(2));
            Assert.That(body.RootElement.GetProperty("stations").GetArrayLength(), Is.EqualTo(2));
            Assert.That(body.RootElement.GetProperty("tableSuggestions").GetArrayLength(), Is.EqualTo(1));
        });

        JsonElement item = body.RootElement.GetProperty("items")[0];

        Assert.Multiple(() =>
        {
            Assert.That(item.GetProperty("priceCents").GetInt32(), Is.EqualTo(350));
            Assert.That(item.GetProperty("stationIds").GetArrayLength(), Is.EqualTo(1));
            Assert.That(item.GetProperty("isAvailable").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task GetCatalog_SoldOutAndDeactivatedItems_KeepsSoldOutAndLeavesDeactivatedOut()
    {
        await using (GastronomyAppDbContext context = factory.CreateContext())
        {
            CatalogItem bratwurst = await context.CatalogItems.FirstAsync(item => item.Id == world.BratwurstItemId);
            bratwurst.IsAvailable = false;

            CatalogItem beer = await context.CatalogItems.FirstAsync(item => item.Id == world.BeerItemId);
            beer.IsActive = false;

            await context.SaveChangesAsync();
        }

        JsonDocument body = await GetCatalogAsync();
        JsonElement items = body.RootElement.GetProperty("items");

        Assert.Multiple(() =>
        {
            Assert.That(items.GetArrayLength(), Is.EqualTo(1));
            Assert.That(items[0].GetProperty("id").GetGuid(), Is.EqualTo(world.BratwurstItemId));
            Assert.That(items[0].GetProperty("isAvailable").GetBoolean(), Is.False);
        });
    }

    [Test]
    public async Task GetCatalog_NoDeviceToken_IsRefused()
    {
        using HttpResponseMessage response = await factory.Client.GetAsync("/api/catalog");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private async Task<JsonDocument> GetCatalogAsync()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/catalog");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);
        using HttpResponseMessage response = await factory.Client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
