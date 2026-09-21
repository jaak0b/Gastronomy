using FakeItEasy;
using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.Hub;
using GastronomyApp.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Tests.Announcers;

[TestFixture]
public sealed class DeviceRevocationAnnouncerTest
{
  [SetUp]
  public void SetUp()
  {
    _clients = A.Fake<IClientProxy>();
    _hubClients = A.Fake<IHubClients>();
    _hubContext = A.Fake<IHubContext<GastronomyHub>>();

    A.CallTo(() => _hubContext.Clients).Returns(_hubClients);
    A.CallTo(() => _hubClients.Group(A<string>._)).Returns(_clients);

    _announcer = new(new(_hubContext), new(new(), _hubContext), new(A.Fake<IHostApplicationLifetime>(), A.Fake<ILogger<SavedChangeAnnouncer>>()));
  }

  private readonly Guid _deviceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

  private DeviceRevocationAnnouncer _announcer = null!;
  private IClientProxy _clients = null!;
  private IHubClients _hubClients = null!;
  private IHubContext<GastronomyHub> _hubContext = null!;

  [Test]
  public async Task AnnounceAsync_ADeviceThatWasJustRevoked_TellsThatDeviceItIsSignedOut()
  {
    await _announcer.AnnounceAsync(_deviceId, CancellationToken.None);

    A.CallTo(() => _clients.SendCoreAsync("DeviceRevoked", A<object[]>.That.Matches(arguments => NamesTheRevokedDevice(arguments)), A<CancellationToken>._)).MustHaveHappened();
  }

  [Test]
  public async Task AnnounceAsync_AHubThatCannotBeReached_LeavesTheRevocationStandingInsteadOfThrowing()
  {
    A.CallTo(() => _clients.SendCoreAsync(A<string>._, A<object[]>._, A<CancellationToken>._)).Throws<InvalidOperationException>();

    Assert.That(async () => await _announcer.AnnounceAsync(_deviceId, CancellationToken.None), Throws.Nothing);
  }

  private bool NamesTheRevokedDevice(IReadOnlyList<object> arguments)
  {
    return arguments.Single() is DeviceRevokedEvent revoked && revoked.DeviceId == _deviceId;
  }
}
