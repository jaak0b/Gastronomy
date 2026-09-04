using System.Net;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class OrderIdempotencyTest
{

  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync(false);
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
    var ticketCount = await database.StationOrders.CountAsync();
    var printJobCount = await database.PrintJobs.CountAsync(job => job.CopyNumber == 0);

    Assert.Multiple(() =>
                    {
                      Assert.That(orderCount, Is.EqualTo(1));
                      Assert.That(ticketCount, Is.EqualTo(1));
                      Assert.That(printJobCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task PostOrder_SameSubmissionIdDifferentContent_IsRefused()
  {
    var clientOrderId = Guid.NewGuid();

    using (var first = await _context.PostOrderAsync(_context.BuildOrder(clientOrderId)))
    {
      Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    OrderBody different = new(clientOrderId,
                              "Tisch 99",
                              null,
                              [new(_context.World.BratwurstItemId, 350, null, null), new(_context.World.BratwurstItemId, 350, null, null)]);

    using var second = await _context.PostOrderAsync(different);

    Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
  }
}
