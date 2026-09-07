using FakeItEasy;
using GastronomyApp.Api.Endpoints;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminStationAnnouncementTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;

  [Test]
  public async Task Update_ANameTheStationDidNotHaveBefore_TellsTheDevices()
  {
    using var scope = _context.Factory.Services.CreateScope();
    var hubContext = A.Fake<IHubContext<GastronomyHub>>();

    await HandlerTalkingTo(scope.ServiceProvider, hubContext)
      .UpdateAsync(_context.World.KitchenStationId,
                   new() { Name = "Kueche am Zelt", SortOrder = 1 },
                   CancellationToken.None);

    A.CallTo(() => hubContext.Clients).MustHaveHappened();
  }

  [Test]
  public async Task Update_TheNameAndPlaceTheStationAlreadyHad_TellsTheDevicesNothingBecauseNothingChanged()
  {
    using var scope = _context.Factory.Services.CreateScope();
    var hubContext = A.Fake<IHubContext<GastronomyHub>>();

    await HandlerTalkingTo(scope.ServiceProvider, hubContext)
      .UpdateAsync(_context.World.KitchenStationId,
                   new() { Name = "Kueche", SortOrder = 1 },
                   CancellationToken.None);

    A.CallTo(() => hubContext.Clients).MustNotHaveHappened();
  }

  [Test]
  public async Task Deactivate_AStationNoItemNeeds_TellsTheDevices()
  {
    using var scope = _context.Factory.Services.CreateScope();
    var hubContext = A.Fake<IHubContext<GastronomyHub>>();
    var stationId = await AddAStationNoItemNeedsAsync(scope.ServiceProvider);

    await HandlerTalkingTo(scope.ServiceProvider, hubContext).DeactivateAsync(stationId, CancellationToken.None);

    A.CallTo(() => hubContext.Clients).MustHaveHappened();
  }

  [Test]
  public async Task Activate_AStationThatWasAlreadySwitchedOn_TellsTheDevicesNothingBecauseNothingChanged()
  {
    using var scope = _context.Factory.Services.CreateScope();
    var hubContext = A.Fake<IHubContext<GastronomyHub>>();

    await HandlerTalkingTo(scope.ServiceProvider, hubContext)
      .ActivateAsync(_context.World.KitchenStationId, CancellationToken.None);

    A.CallTo(() => hubContext.Clients).MustNotHaveHappened();
  }

  private async Task<Guid> AddAStationNoItemNeedsAsync(IServiceProvider services)
  {
    var dbContext = services.GetRequiredService<GastronomyAppDbContext>();
    Station station = new()
                      {
                        Id = Guid.NewGuid(),
                        Name = "Kuchenbuffet",
                        SortOrder = 3,
                        IsActive = true,
                        NextStationOrderNumber = 1
                      };

    dbContext.Stations.Add(station);
    await dbContext.SaveChangesAsync(CancellationToken.None);

    return station.Id;
  }

  private AdminStationHandler HandlerTalkingTo(IServiceProvider services, IHubContext<GastronomyHub> hubContext)
  {
    StationChangeAnnouncer announcer =
      new(new(hubContext, services.GetRequiredService<IDbContextFactory<GastronomyAppDbContext>>()),
          new(services.GetRequiredService<IHostApplicationLifetime>(),
              A.Fake<ILogger<SavedChangeAnnouncement>>()));

    return new(services.GetRequiredService<GastronomyAppDbContext>(),
               services.GetRequiredService<OutstandingInvitationLookup>(),
               services.GetRequiredService<DeviceRevoker>(),
               announcer,
               services.GetRequiredService<ResultEnvelope>(),
               services.GetRequiredService<IClock>());
  }
}
