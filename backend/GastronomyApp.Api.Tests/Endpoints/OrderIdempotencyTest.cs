using System.Net;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class OrderIdempotencyTest
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
  public async Task PostOrder_SameSubmissionIdAndContent_ReturnsTheOriginalBodyByteForByte()
  {
    var body = _context.BuildOrder(Guid.NewGuid());

    string firstBody;
    using (var first = await _context.PostOrderAsync(body))
    {
      firstBody = await first.Content.ReadAsStringAsync();
      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    string secondBody;
    using (var second = await _context.PostOrderAsync(body))
    {
      secondBody = await second.Content.ReadAsStringAsync();
      Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    Assert.That(secondBody, Is.EqualTo(firstBody));

    await using var database = _context.Factory.CreateContext();
    var orderCount = await database.Orders.CountAsync();
    var stationOrderCount = await database.StationOrders.CountAsync();
    var itemCount = await database.OrderItems.CountAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(orderCount, Is.EqualTo(1));
                      Assert.That(stationOrderCount, Is.EqualTo(1));
                      Assert.That(itemCount, Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task PostOrder_SameSubmissionIdDifferentContent_ReturnsTheOrderTheLaptopAlreadyHolds()
  {
    var clientOrderId = Guid.NewGuid();

    string firstBody;
    using (var first = await _context.PostOrderAsync(_context.BuildOrder(clientOrderId)))
    {
      firstBody = await first.Content.ReadAsStringAsync();
      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    OrderBody different = new(clientOrderId,
                              "Tisch 99",
                              null,
                              [new(_context.World.BratwurstItemId, 350, null, null)]);

    string secondBody;
    using (var second = await _context.PostOrderAsync(different))
    {
      secondBody = await second.Content.ReadAsStringAsync();
      Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    Assert.That(secondBody, Is.EqualTo(firstBody));

    await using var database = _context.Factory.CreateContext();

    var orderCount = await database.Orders.CountAsync();
    var itemCount = await database.OrderItems.CountAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(orderCount, Is.EqualTo(1));
                      Assert.That(itemCount, Is.EqualTo(2));
                    });
  }
}

