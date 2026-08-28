using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Auth;

[TestFixture]
public sealed class DeviceAuthenticationTest
{
  private ApiTestFactory factory = null!;
  private SeededWorld world = null!;

  [SetUp]
  public async Task SetUp()
  {
    factory = await new ApiTestFactory.Builder().StartAsync();
    await using GastronomyAppDbContext context = factory.CreateContext();
    world = await new ApiSeeder().SeedAsync(context, CancellationToken.None);
  }

  [TearDown]
  public async Task TearDown()
  {
    await factory.DisposeAsync();
  }

  [Test]
  public async Task Authenticate_NoAuthorizationHeader_IsRefused()
  {
    using HttpResponseMessage response = await factory.Client.GetAsync("/api/session");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  [TestCase("nodothere")]
  [TestCase("unknownlookup.secret")]
  public async Task Authenticate_MalformedOrUnknownToken_IsRefused(string token)
  {
    using HttpRequestMessage request = new(HttpMethod.Get, "/api/session");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    using HttpResponseMessage response = await factory.Client.SendAsync(request);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  [Test]
  public async Task Authenticate_AuthorizationHeaderWithoutTheBearerPrefix_IsRefused()
  {
    IssuedDeviceToken issued = await IssueTokenAsync();

    using HttpRequestMessage request = new(HttpMethod.Get, "/api/session");
    request.Headers.TryAddWithoutValidation("Authorization", issued.PlaintextToken);

    using HttpResponseMessage response = await factory.Client.SendAsync(request);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  [Test]
  public async Task Authenticate_RevokedDeviceToken_IsRefused()
  {
    IssuedDeviceToken issued = await IssueTokenAsync();

    using (IServiceScope scope = factory.Services.CreateScope())
    {
      await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
          .RevokeAsync(issued.Device.Id, CancellationToken.None);
    }

    using HttpRequestMessage request = new(HttpMethod.Get, "/api/session");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", issued.PlaintextToken);

    using HttpResponseMessage response = await factory.Client.SendAsync(request);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
  }

  [Test]
  public async Task Authenticate_ValidToken_ResolvesTheStaffMemberBehindTheDevice()
  {
    IssuedDeviceToken issued = await IssueTokenAsync();

    using HttpRequestMessage request = new(HttpMethod.Get, "/api/session");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", issued.PlaintextToken);

    using HttpResponseMessage response = await factory.Client.SendAsync(request);
    JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Multiple(() =>
    {
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
      Assert.That(
              body.RootElement.GetProperty("deviceId").GetGuid(),
              Is.EqualTo(issued.Device.Id));
      Assert.That(
              body.RootElement.GetProperty("staffMember").GetProperty("id").GetGuid(),
              Is.EqualTo(world.StaffMemberId));
    });
  }

  private async Task<IssuedDeviceToken> IssueTokenAsync()
  {
    using IServiceScope scope = factory.Services.CreateScope();
    return await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
        .IssueAsync(world.StaffMemberId, "de", "NUnit", CancellationToken.None);
  }
}
