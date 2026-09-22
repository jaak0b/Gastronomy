using FakeItEasy;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Mapping;
using GastronomyApp.Contracts.Events;
using MapsterMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Tests.Hub;

[TestFixture]
public sealed class HubNotificationDispatcherTest
{
  [SetUp]
  public void SetUp()
  {
    _clients = A.Fake<IClientProxy>();
    _hubClients = A.Fake<IHubClients>();
    _hubContext = A.Fake<IHubContext<GastronomyHub>>();

    A.CallTo(() => _hubContext.Clients).Returns(_hubClients);
    A.CallTo(() => _hubClients.Group(A<string>._)).Returns(_clients);

    AnnouncementGuard guard = new(A.Fake<IHostApplicationLifetime>(), A.Fake<ILogger<AnnouncementGuard>>());

    IMappingRegistration[] registrations =
    [
      new AdminCategoryMapping(),
      new AdminFestivalMapping(new()),
      new AdminItemMapping(),
      new AdminStaffMembersMapping(new()),
      new AdminStationMapping(new()),
      new CatalogMapping(),
      new EnrolmentMapping(),
      new OpenItemMapping(),
      new OrderMapping(new()),
      new SettlementMapping(),
      new StationEstimateMapping(new()),
      new StationQueueMapping(new())
    ];

    _dispatcher = new(_hubContext, guard, new(new(), _hubContext), new Mapper(new MappingConfiguration(registrations).Build()));
  }

  private readonly Guid _deviceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
  private readonly Guid _stationId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

  private IClientProxy _clients = null!;
  private HubNotificationDispatcher _dispatcher = null!;
  private IHubClients _hubClients = null!;
  private IHubContext<GastronomyHub> _hubContext = null!;

  [Test]
  public async Task AnnounceDeviceRevokedAsync_ADeviceThatWasJustRevoked_TellsThatDeviceItIsSignedOut()
  {
    await _dispatcher.AnnounceDeviceRevokedAsync(_deviceId, CancellationToken.None);

    A.CallTo(() => _clients.SendCoreAsync("DeviceRevoked", A<object[]>.That.Matches(arguments => NamesTheRevokedDevice(arguments)), A<CancellationToken>._)).MustHaveHappened();
  }

  [Test]
  public void AnnounceDeviceRevokedAsync_AHubThatCannotBeReached_LeavesTheRevocationStandingInsteadOfThrowing()
  {
    A.CallTo(() => _clients.SendCoreAsync(A<string>._, A<object[]>._, A<CancellationToken>._)).Throws<InvalidOperationException>();

    Assert.That(async () => await _dispatcher.AnnounceDeviceRevokedAsync(_deviceId, CancellationToken.None), Throws.Nothing);
  }

  [Test]
  public async Task AnnounceStationOrdersChangedAsync_AStationThatJustGotAnOrder_TellsThatStation()
  {
    await _dispatcher.AnnounceStationOrdersChangedAsync(_stationId, CancellationToken.None);

    A.CallTo(() => _clients.SendCoreAsync("StationOrdersChanged", A<object[]>._, A<CancellationToken>._)).MustHaveHappened();
  }

  private bool NamesTheRevokedDevice(IReadOnlyList<object> arguments)
  {
    return arguments.Single() is DeviceRevokedEvent revoked && revoked.DeviceId == _deviceId;
  }
}
