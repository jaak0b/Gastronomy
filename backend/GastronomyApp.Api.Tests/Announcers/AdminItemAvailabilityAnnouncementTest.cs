using FakeItEasy;
using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Handlers;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Tests.TestSupport;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Tests.Announcers;

[TestFixture]
public sealed class AdminItemAvailabilityAnnouncementTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContextBuilder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;

  [Test]
  public async Task SetAvailability_ToTheValueTheItemAlreadyHas_TellsTheDevicesNothingBecauseNothingChanged()
  {
    using var scope = _context.Factory.Services.CreateScope();
    IHubContext<GastronomyHub> hubContext = A.Fake<IHubContext<GastronomyHub>>();

    await HandlerTalkingTo(scope.ServiceProvider, hubContext).SetAvailabilityAsync(_context.World.FestivalId, _context.World.BratwurstItemId, new() { IsAvailable = true }, CancellationToken.None);

    A.CallTo(() => hubContext.Clients).MustNotHaveHappened();
  }

  [Test]
  public async Task SetAvailability_ToSoldOut_TellsTheDevicesTheCatalogChanged()
  {
    using var scope = _context.Factory.Services.CreateScope();
    IHubContext<GastronomyHub> hubContext = A.Fake<IHubContext<GastronomyHub>>();

    await HandlerTalkingTo(scope.ServiceProvider, hubContext).SetAvailabilityAsync(_context.World.FestivalId, _context.World.BratwurstItemId, new() { IsAvailable = false }, CancellationToken.None);

    A.CallTo(() => hubContext.Clients).MustHaveHappened();
  }

  private AdminFestivalMenuHandler HandlerTalkingTo(IServiceProvider services, IHubContext<GastronomyHub> hubContext)
  {
    CatalogChangeAnnouncer announcer = new(new(hubContext));

    return new(services.GetRequiredService<FestivalMenuService>(), announcer, new(services.GetRequiredService<IHostApplicationLifetime>(), A.Fake<ILogger<SavedChangeAnnouncer>>()), services.GetRequiredService<ResultEnvelope>(), A.Fake<ILogger<AdminFestivalMenuHandler>>());
  }
}
