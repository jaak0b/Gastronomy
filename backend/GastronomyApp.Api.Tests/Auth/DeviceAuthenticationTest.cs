using System.Net;
using System.Text.Json;
using GastronomyApp.Api.Tests.TestSupport;
using GastronomyApp.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using GastronomyApp.Infrastructure.Persistence;

namespace GastronomyApp.Api.Tests.Auth;

[TestFixture]
public sealed class DeviceAuthenticationTest
{
  [SetUp]
  public async Task SetUp()
  {
    _factory = await new ApiTestFactoryBuilder().StartAsync();
    await using var context = _factory.CreateContext();
    _world = await new ApiSeeder().SeedAsync(context, CancellationToken.None);
  }

  [TearDown]
  public async Task TearDown()
  {
    await _factory.DisposeAsync();
  }

  private ApiTestFactory _factory = null!;
  private SeededWorld _world = null!;

  [Test]
  public async Task Authenticate_NoAuthorizationHeader_IsRefused()
  {
    using var response = await _factory.Client.GetAsync("/api/session");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  [TestCase("nodothere")]
  [TestCase("unknownlookup.secret")]
  public async Task Authenticate_MalformedOrUnknownToken_IsRefused(string token)
  {
    using HttpRequestMessage request = new(HttpMethod.Get, "/api/session");
    request.Headers.Authorization = new("Bearer", token);

    using var response = await _factory.Client.SendAsync(request);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  [Test]
  public async Task Authenticate_AuthorizationHeaderWithoutTheBearerPrefix_IsRefused()
  {
    var issued = await IssueTokenAsync();

    using HttpRequestMessage request = new(HttpMethod.Get, "/api/session");
    request.Headers.TryAddWithoutValidation("Authorization", issued.PlaintextToken);

    using var response = await _factory.Client.SendAsync(request);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  [Test]
  public async Task Authenticate_RevokedDeviceToken_IsRefused()
  {
    var issued = await IssueTokenAsync();

    using (var scope = _factory.Services.CreateScope())
    {
      scope.ServiceProvider.GetRequiredService<AfterCommitActions>().StartCollecting();
      await scope.ServiceProvider.GetRequiredService<DeviceOwnerRetirement>().RevokeDeviceAsync(issued.DeviceId, CancellationToken.None);
    }

    using HttpRequestMessage request = new(HttpMethod.Get, "/api/session");
    request.Headers.Authorization = new("Bearer", issued.PlaintextToken);

    using var response = await _factory.Client.SendAsync(request);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  [Test]
  public async Task Authenticate_ValidToken_ResolvesTheStaffMemberBehindTheDevice()
  {
    var issued = await IssueTokenAsync();

    using HttpRequestMessage request = new(HttpMethod.Get, "/api/session");
    request.Headers.Authorization = new("Bearer", issued.PlaintextToken);

    using var response = await _factory.Client.SendAsync(request);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                      Assert.That(body.RootElement.GetProperty("deviceId").GetGuid(), Is.EqualTo(issued.DeviceId));
                      Assert.That(body.RootElement.GetProperty("staffMember").GetProperty("id").GetGuid(), Is.EqualTo(_world.StaffMemberId));
                    });
  }

  [Test]
  public async Task Authenticate_AWrongSecretForAnIssuedToken_IsRefused()
  {
    var issued = await IssueTokenAsync();
    var tokenLookupId = issued.PlaintextToken[..issued.PlaintextToken.IndexOf('.', StringComparison.Ordinal)];

    using var response = await GetSessionAsync($"{tokenLookupId}.wrong-secret");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  private async Task<EnrolledDevice> IssueTokenAsync()
  {
    return await new TestDeviceEnrolment(_factory.Services).EnrolStaffMemberAsync(_world.StaffMemberId);
  }

  private async Task<HttpResponseMessage> GetSessionAsync(string deviceToken)
  {
    using HttpRequestMessage request = new(HttpMethod.Get, "/api/session");
    request.Headers.Authorization = new("Bearer", deviceToken);

    return await _factory.Client.SendAsync(request);
  }
}
