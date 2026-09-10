using System.Text.Json;
using FakeItEasy;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.Endpoints;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class OrderItemSettlementNotificationTest
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
  public async Task Settle_WhenTheOtherPhonesCannotBeTold_KeepsTheSettlementAndSaysOnlyTheNoticeFailed()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync();
    using var scope = _context.Factory.Services.CreateScope();

    var result = await HandlerThatCannotReachTheOtherPhones(scope.ServiceProvider)
                   .SettleAsync(new() { OrderItemIds = itemIds, AmountPaidCents = 700, PaymentNotice = null },
                                new(_context.World.StaffMemberId, _context.DeviceId, "de"),
                                CancellationToken.None);

    var view = ((Ok<SettlementView>)result).Value!;
    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(view.OtherPhonesWereTold, Is.False);
                      Assert.That(view.SettledOrderItemIds, Has.Count.EqualTo(2));
                      Assert.That(stored.Select(item => item.SettledAtUtc), Is.All.Not.Null);
                      Assert.That(stored.Select(item => item.ChargedPriceCents), Is.All.EqualTo(350));
                    });
  }

  private OrderItemSettlementHandler HandlerThatCannotReachTheOtherPhones(IServiceProvider services)
  {
    var hubContext = A.Fake<IHubContext<GastronomyHub>>();
    A.CallTo(() => hubContext.Clients).Throws(new InvalidOperationException("the hub is not answering"));

    return new(services.GetRequiredService<GastronomyAppDbContext>(),
               services.GetRequiredService<OrderItemSettlementService>(),
               services.GetRequiredService<OpenItemsReader>(),
               new(hubContext, services.GetRequiredService<IDbContextFactory<GastronomyAppDbContext>>()),
               services.GetRequiredService<ResultEnvelope>(),
               services.GetRequiredService<RunningFestivalLookup>(),
               services.GetRequiredService<IClock>(),
               NullLogger<OrderItemSettlementHandler>.Instance);
  }

  private async Task<IReadOnlyList<Guid>> PlaceOrderAsync()
  {
    using var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return
    [
      .. body.RootElement
             .GetProperty("stationOrders")
             .EnumerateArray()
             .SelectMany(stationOrder => stationOrder.GetProperty("itemIds").EnumerateArray())
             .Select(itemId => itemId.GetGuid())
    ];
  }
}
