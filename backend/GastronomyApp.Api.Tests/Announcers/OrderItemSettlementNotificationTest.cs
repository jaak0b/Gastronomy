using System.Text.Json;
using FakeItEasy;
using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Handlers;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Tests.TestSupport;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GastronomyApp.Api.Tests.Announcers;

[TestFixture]
public sealed class OrderItemSettlementNotificationTest
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
  public async Task Settle_WhenTheOtherPhonesCannotBeTold_KeepsTheSettlementAndSaysOnlyTheNoticeFailed()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync();
    using var scope = _context.Factory.Services.CreateScope();

    var result = await HandlerThatCannotReachTheOtherPhones(scope.ServiceProvider, NullLogger<OrderItemSettlementHandler>.Instance)
                .SettleAsync(new()
                             {
                               Lines =
                               [
                                 new()
                                 {
                                   OrderItemId = itemIds[0],
                                   PaidPriceCents = 350
                                 },
                                 new()
                                 {
                                   OrderItemId = itemIds[1],
                                   PaidPriceCents = 350
                                 }
                               ]
                             },
                             new(_context.World.StaffMemberId, _context.DeviceId, "de"),
                             CancellationToken.None);

    var view = ((Ok<SettlementView>)result).Value!;
    await using var database = _context.Factory.CreateContext();
    List<OrderItem> stored = await database.OrderItems.ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(view.OtherPhonesWereTold, Is.False);
                      Assert.That(view.SettledOrderItemIds, Has.Count.EqualTo(2));
                      Assert.That(view.ReappliedOrderItemIds, Is.Empty);
                      Assert.That(view.AlreadySettledByOthersOrderItemIds, Is.Empty);
                      Assert.That(stored.Select(item => item.SettledAtUtc), Is.All.Not.Null);
                      Assert.That(stored.Select(item => item.ChargedPriceCents), Is.All.EqualTo(350));
                    });
  }

  [Test]
  public async Task SettleAsync_ItemsWereSettled_NamesTheirIdsAndTheirCountInTheLog()
  {
    IReadOnlyList<Guid> itemIds = await PlaceOrderAsync();
    using var scope = _context.Factory.Services.CreateScope();
    ILogger<OrderItemSettlementHandler> logger = A.Fake<ILogger<OrderItemSettlementHandler>>();

    await HandlerThatCannotReachTheOtherPhones(scope.ServiceProvider, logger)
   .SettleAsync(new()
                {
                  Lines =
                  [
                    new()
                    {
                      OrderItemId = itemIds[0],
                      PaidPriceCents = 350
                    },
                    new()
                    {
                      OrderItemId = itemIds[1],
                      PaidPriceCents = 350
                    }
                  ]
                },
                new(_context.World.StaffMemberId, _context.DeviceId, "de"),
                CancellationToken.None);

    A.CallTo(logger)
     .Where(call => call.Method.Name == nameof(ILogger.Log)
                    && call.GetArgument<LogLevel>(0) == LogLevel.Information
                    && Equals(ValueNamed(call.GetArgument<object>(2), "SettledItemCount"), 2)
                    && SettledIdsIn(call.GetArgument<object>(2)).Contains(itemIds[0])
                    && SettledIdsIn(call.GetArgument<object>(2)).Contains(itemIds[1]))
     .MustHaveHappened();
  }

  private object? ValueNamed(object? state, string name)
  {
    if (state is not IReadOnlyList<KeyValuePair<string, object?>> values)
      return null;

    return values.FirstOrDefault(value => value.Key == name).Value;
  }

  private IReadOnlyList<Guid> SettledIdsIn(object? state)
  {
    return ValueNamed(state, "SettledOrderItemIds") as IReadOnlyList<Guid> ?? [];
  }

  private OrderItemSettlementHandler HandlerThatCannotReachTheOtherPhones(IServiceProvider services, ILogger<OrderItemSettlementHandler> logger)
  {
    IHubContext<GastronomyHub> hubContext = A.Fake<IHubContext<GastronomyHub>>();
    A.CallTo(() => hubContext.Clients).Throws(new InvalidOperationException("the hub is not answering"));

    return new(services.GetRequiredService<OrderItemSettlementService>(), services.GetRequiredService<SavedChangeAnnouncer>(), new(hubContext), services.GetRequiredService<ResultEnvelope>(), logger);
  }

  private async Task<IReadOnlyList<Guid>> PlaceOrderAsync()
  {
    using var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("stationOrders").EnumerateArray().SelectMany(stationOrder => stationOrder.GetProperty("itemIds").EnumerateArray()).Select(itemId => itemId.GetGuid()).ToList();
  }
}
