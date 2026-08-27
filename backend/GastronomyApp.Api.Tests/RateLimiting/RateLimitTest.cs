using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.RateLimiting;

[TestFixture]
public sealed class RateLimitTest
{
    private const int DeviceRequestsPerMinute = 600;
    private const int AddressRequestsPerMinute = 20;

    private ApiTestFactory factory = null!;
    private string deviceToken = null!;

    [SetUp]
    public async Task SetUp()
    {
        factory = await new ApiTestFactory.Builder().StartAsync();

        SeededWorld world;
        await using (GastronomyAppDbContext context = factory.CreateContext())
        {
            world = await new ApiSeeder().SeedAsync(context, CancellationToken.None);
        }

        using IServiceScope scope = factory.Services.CreateScope();
        IssuedDeviceToken issued = await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
            .IssueAsync(world.ServerPersonId, "de", "NUnit", CancellationToken.None);
        deviceToken = issued.PlaintextToken;
    }

    [TearDown]
    public async Task TearDown()
    {
        await factory.DisposeAsync();
    }

    [Test]
    public async Task DeviceScopedEndpoint_OneRequestPastTheMinuteLimit_IsRefusedAsTooManyRequests()
    {
        for (int request = 0; request < DeviceRequestsPerMinute; request++)
        {
            using HttpResponseMessage allowed = await SendSessionRequestAsync();

            Assert.That(allowed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        using HttpResponseMessage refused = await SendSessionRequestAsync();
        JsonDocument body = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(refused.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
            Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("review.tooManyRequests"));
        });
    }

    [Test]
    public async Task EnrolmentRedeem_OneRequestPastTheMinuteLimit_IsRefusedAsTooManyRequests()
    {
        for (int request = 0; request < AddressRequestsPerMinute; request++)
        {
            using HttpResponseMessage allowed = await SendRedeemRequestAsync();

            Assert.That(allowed.StatusCode, Is.Not.EqualTo(HttpStatusCode.TooManyRequests));
        }

        using HttpResponseMessage refused = await SendRedeemRequestAsync();
        JsonDocument body = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(refused.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
            Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("review.tooManyRequests"));
        });
    }

    private Task<HttpResponseMessage> SendRedeemRequestAsync()
    {
        return factory.Client.PostAsJsonAsync(
            "/api/enrolment/redeem",
            new Endpoints.RedeemBody(null, "000000", "Anna", "NUnit"));
    }

    private async Task<HttpResponseMessage> SendSessionRequestAsync()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);

        return await factory.Client.SendAsync(request);
    }
}
