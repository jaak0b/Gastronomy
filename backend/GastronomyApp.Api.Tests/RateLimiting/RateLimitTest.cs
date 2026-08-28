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
            .IssueAsync(world.StaffMemberId, "de", "NUnit", CancellationToken.None);
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
        IReadOnlyList<HttpResponseMessage> responses = await SendConcurrentlyAsync(DeviceRequestsPerMinute + 1);

        int allowed = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
        int refused = responses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests);
        HttpResponseMessage? firstRefusal = responses
            .FirstOrDefault(response => response.StatusCode == HttpStatusCode.TooManyRequests);

        string refusalBody = firstRefusal is null ? string.Empty : await firstRefusal.Content.ReadAsStringAsync();

        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }

        Assert.Multiple(() =>
        {
            Assert.That(allowed, Is.EqualTo(DeviceRequestsPerMinute), "The window grants exactly its permit count.");
            Assert.That(refused, Is.EqualTo(1));
            Assert.That(
                JsonDocument.Parse(refusalBody).RootElement.GetProperty("messageKey").GetString(),
                Is.EqualTo("review.tooManyRequests"));
        });
    }

    private async Task<IReadOnlyList<HttpResponseMessage>> SendConcurrentlyAsync(int requestCount)
    {
        List<Task<HttpResponseMessage>> inFlight = [];

        for (int request = 0; request < requestCount; request++)
        {
            inFlight.Add(SendSessionRequestAsync());
        }

        return await Task.WhenAll(inFlight);
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
            new Endpoints.RedeemBody("not-a-real-code", "Anna", "NUnit"));
    }

    private async Task<HttpResponseMessage> SendSessionRequestAsync()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);

        return await factory.Client.SendAsync(request);
    }
}
