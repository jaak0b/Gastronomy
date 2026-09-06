using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.Tests.Endpoints;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.RateLimiting;

[TestFixture]
public sealed class RateLimitTest
{

  [SetUp]
  public async Task SetUp()
  {
    _factory = await new ApiTestFactory.Builder().StartAsync();

    SeededWorld world;
    await using (var context = _factory.CreateContext())
    {
      world = await new ApiSeeder().SeedAsync(context, CancellationToken.None);
    }

    using var scope = _factory.Services.CreateScope();
    var issued = await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
                            .IssueAsync(new(DeviceOwnerKind.StaffMember, world.StaffMemberId), "de", "NUnit", CancellationToken.None);
    _deviceToken = issued.PlaintextToken;
  }

  [TearDown]
  public async Task TearDown()
  {
    await _factory.DisposeAsync();
  }

  private const int DeviceRequestsPerMinute = 600;
  private const int AddressRequestsPerMinute = 20;

  private ApiTestFactory _factory = null!;
  private string _deviceToken = null!;

  [Test]
  public async Task DeviceScopedEndpoint_OneRequestPastTheMinuteLimit_IsRefusedAsTooManyRequests()
  {
    IReadOnlyList<HttpResponseMessage> responses = await SendConcurrentlyAsync(DeviceRequestsPerMinute + 1);

    var allowed = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
    var refused = responses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests);
    var firstRefusal = responses
     .FirstOrDefault(response => response.StatusCode == HttpStatusCode.TooManyRequests);

    var refusalBody = firstRefusal is null ? string.Empty : await firstRefusal.Content.ReadAsStringAsync();

    foreach (var response in responses)
    {
      response.Dispose();
    }

    Assert.Multiple(() =>
                    {
                      Assert.That(allowed, Is.EqualTo(DeviceRequestsPerMinute), "The window grants exactly its permit count.");
                      Assert.That(refused, Is.EqualTo(1));
                      Assert.That(JsonDocument.Parse(refusalBody).RootElement.GetProperty("messageKey").GetString(),
                                  Is.EqualTo("session.tooManyRequests"));
                    });
  }

  private async Task<IReadOnlyList<HttpResponseMessage>> SendConcurrentlyAsync(int requestCount)
  {
    List<Task<HttpResponseMessage>> inFlight = [];

    for (var request = 0; request < requestCount; request++)
    {
      inFlight.Add(SendSessionRequestAsync());
    }

    return await Task.WhenAll(inFlight);
  }

  [Test]
  public async Task EnrolmentRedeem_OneRequestPastTheMinuteLimit_IsRefusedAsTooManyRequests()
  {
    for (var request = 0; request < AddressRequestsPerMinute; request++)
    {
      using var allowed = await SendRedeemRequestAsync();

      Assert.That(allowed.StatusCode, Is.Not.EqualTo(HttpStatusCode.TooManyRequests));
    }

    using var refused = await SendRedeemRequestAsync();
    var body = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(refused.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
                      Assert.That(body.RootElement.GetProperty("messageKey").GetString(), Is.EqualTo("session.tooManyRequests"));
                    });
  }

  private Task<HttpResponseMessage> SendRedeemRequestAsync()
  {
    return _factory.Client.PostAsJsonAsync("/api/enrolment/redeem",
                                          new RedeemBody("not-a-real-code", "Anna", "NUnit"));
  }

  private async Task<HttpResponseMessage> SendSessionRequestAsync()
  {
    using HttpRequestMessage request = new(HttpMethod.Get, "/api/session");
    request.Headers.Authorization = new("Bearer", _deviceToken);

    return await _factory.Client.SendAsync(request);
  }
}

