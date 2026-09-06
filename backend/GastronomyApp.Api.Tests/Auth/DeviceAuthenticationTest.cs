using System.Net;
using System.Text.Json;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Auth;

[TestFixture]
public sealed class DeviceAuthenticationTest
{

  [SetUp]
  public async Task SetUp()
  {
    _factory = await new ApiTestFactory.Builder().StartAsync();
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
      await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
                 .RevokeAsync(issued.Device.Id, CancellationToken.None);
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
                      Assert.That(body.RootElement.GetProperty("deviceId").GetGuid(),
                                  Is.EqualTo(issued.Device.Id));
                      Assert.That(body.RootElement.GetProperty("staffMember").GetProperty("id").GetGuid(),
                                  Is.EqualTo(_world.StaffMemberId));
                    });
  }

  private async Task<IssuedDeviceToken> IssueTokenAsync()
  {
    using var scope = _factory.Services.CreateScope();
    return await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
                      .IssueAsync(new(DeviceOwnerKind.StaffMember, _world.StaffMemberId), "de", "NUnit", CancellationToken.None);
  }
}

