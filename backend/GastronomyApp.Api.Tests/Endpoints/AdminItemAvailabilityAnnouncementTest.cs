using FakeItEasy;
using GastronomyApp.Api.Endpoints;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminItemAvailabilityAnnouncementTest
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
  public async Task SetAvailability_ToTheValueTheItemAlreadyHas_TellsTheDevicesNothingBecauseNothingChanged()
  {
    using var scope = _context.Factory.Services.CreateScope();
    var hubContext = A.Fake<IHubContext<GastronomyHub>>();

    await HandlerTalkingTo(scope.ServiceProvider, hubContext)
      .SetAvailabilityAsync(_context.World.BratwurstItemId,
                            new() { IsAvailable = true },
                            CancellationToken.None);

    A.CallTo(() => hubContext.Clients).MustNotHaveHappened();
  }

  [Test]
  public async Task SetAvailability_ToSoldOut_TellsTheDevicesTheCatalogChanged()
  {
    using var scope = _context.Factory.Services.CreateScope();
    var hubContext = A.Fake<IHubContext<GastronomyHub>>();

    await HandlerTalkingTo(scope.ServiceProvider, hubContext)
      .SetAvailabilityAsync(_context.World.BratwurstItemId,
                            new() { IsAvailable = false },
                            CancellationToken.None);

    A.CallTo(() => hubContext.Clients).MustHaveHappened();
  }

  private AdminItemHandler HandlerTalkingTo(IServiceProvider services, IHubContext<GastronomyHub> hubContext)
  {
    CatalogChangeAnnouncer announcer =
      new(new(hubContext, services.GetRequiredService<IDbContextFactory<GastronomyAppDbContext>>()));

    return new(services.GetRequiredService<GastronomyAppDbContext>(),
               new(announcer,
                   new(services.GetRequiredService<IHostApplicationLifetime>(),
                       A.Fake<ILogger<SavedChangeAnnouncement>>())),
               services.GetRequiredService<ResultEnvelope>());
  }
}
