using FakeItEasy;
using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Handlers;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Tests.TestSupport;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Tests.Handlers;

[TestFixture]
public sealed class AdminFestivalHandlerTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContextBuilder().StartAsync();
    _scope = _context.Factory.Services.CreateScope();
  }

  [TearDown]
  public async Task TearDown()
  {
    _scope.Dispose();
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;
  private IServiceScope _scope = null!;

  [Test]
  public async Task CreateAsync_APeriodNoOtherFestivalCovers_TellsTheDevices()
  {
    IHubContext<GastronomyHub> hubContext = A.Fake<IHubContext<GastronomyHub>>();

    var response = await HandlerTalkingTo(hubContext).CreateAsync(BuildNextYearsFestival(), CancellationToken.None);

    Assert.That(response, Is.Not.Null);

    A.CallTo(() => hubContext.Clients).MustHaveHappened();
  }

  [Test]
  public async Task CreateAsync_TheDevicesCannotBeTold_StillAnswersTheAdminWithTheSavedChange()
  {
    var proxy = A.Fake<IClientProxy>();
    A.CallTo(() => proxy.SendCoreAsync(A<string>._, A<object?[]>._, A<CancellationToken>._)).Throws(new InvalidOperationException("The connection to the station tablets broke."));

    var response = await HandlerTalkingTo(HubTalkingTo(proxy)).CreateAsync(BuildNextYearsFestival(), CancellationToken.None);

    Assert.That(response, Is.InstanceOf<IStatusCodeHttpResult>());
    Assert.That(((IStatusCodeHttpResult)response).StatusCode, Is.EqualTo(StatusCodes.Status201Created));
  }

  [Test]
  public async Task HideAsync_AFestivalRunningRightNow_RefusesAndTellsNobody()
  {
    IHubContext<GastronomyHub> hubContext = A.Fake<IHubContext<GastronomyHub>>();

    var response = await HandlerTalkingTo(hubContext).HideAsync(_context.World.FestivalId, CancellationToken.None);

    Assert.That(((IStatusCodeHttpResult)response).StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));

    A.CallTo(() => hubContext.Clients).MustNotHaveHappened();
  }

  private SaveFestivalRequest BuildNextYearsFestival()
  {
    return new()
           {
             Name = "Sommerfest 2028",
             StartsAtUtc = DateTime.UtcNow.AddYears(2),
             EndsAtUtc = DateTime.UtcNow.AddYears(2).AddDays(1)
           };
  }

  private IHubContext<GastronomyHub> HubTalkingTo(IClientProxy proxy)
  {
    var clients = A.Fake<IHubClients>();
    A.CallTo(() => clients.Group(A<string>._)).Returns(proxy);

    IHubContext<GastronomyHub> hubContext = A.Fake<IHubContext<GastronomyHub>>();
    A.CallTo(() => hubContext.Clients).Returns(clients);

    return hubContext;
  }

  private AdminFestivalHandler HandlerTalkingTo(IHubContext<GastronomyHub> hubContext)
  {
    FestivalChangeAnnouncer announcer = new(new(hubContext), new(_scope.ServiceProvider.GetRequiredService<IHostApplicationLifetime>(), A.Fake<ILogger<SavedChangeAnnouncer>>()));

    return new(_scope.ServiceProvider.GetRequiredService<FestivalAdministrationService>(), announcer, _scope.ServiceProvider.GetRequiredService<ResultEnvelope>());
  }
}
