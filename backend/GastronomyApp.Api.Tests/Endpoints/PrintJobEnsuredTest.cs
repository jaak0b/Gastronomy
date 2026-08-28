using System.Text.Json;
using GastronomyApp.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class PrintJobEnsuredTest
{

  [SetUp]
  public async Task SetUp()
  {
    context = await new OrderTestContext.Builder().StartAsync(false);
  }

  [TearDown]
  public async Task TearDown()
  {
    await context.DisposeAsync();
  }

  private OrderTestContext context = null!;

  [Test]
  public async Task PostOrder_RetriedAfterATicketWasNeverHandedToAPrinter_HandsItOverOnTheRetry()
  {
    var clientOrderId = Guid.NewGuid();
    using var first = await context.PostOrderAsync(context.BuildOrder(clientOrderId));
    var body = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
    var stationOrderId = body.RootElement.GetProperty("stationOrders")[0].GetProperty("stationOrderId").GetGuid();

    await using (var database = context.Factory.CreateContext())
    {
      await database.PrintJobs
                    .Where(job => job.StationOrderId == stationOrderId)
                    .ExecuteDeleteAsync();
    }

    using var retry = await context.PostOrderAsync(context.BuildOrder(clientOrderId));

    await using var verification = context.Factory.CreateContext();
    Assert.That(await verification.PrintJobs.CountAsync(job => job.StationOrderId == stationOrderId),
                Is.EqualTo(1));
  }

  [Test]
  public async Task PostOrder_RetriedWhileTheSlipIsStillWaiting_DoesNotQueueASecondPrint()
  {
    var clientOrderId = Guid.NewGuid();
    using var first = await context.PostOrderAsync(context.BuildOrder(clientOrderId));
    var body = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
    var stationOrderId = body.RootElement.GetProperty("stationOrders")[0].GetProperty("stationOrderId").GetGuid();

    using var retry = await context.PostOrderAsync(context.BuildOrder(clientOrderId));

    await using var verification = context.Factory.CreateContext();
    Assert.That(await verification.PrintJobs.CountAsync(job => job.StationOrderId == stationOrderId),
                Is.EqualTo(1));
  }

  [Test]
  public async Task PostReprint_TwoSimultaneousTaps_QueuesAtMostOneJob()
  {
    using var created = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid()));
    var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
    var orderId = body.RootElement.GetProperty("orderId").GetGuid();
    var stationOrderId = body.RootElement.GetProperty("stationOrders")[0].GetProperty("stationOrderId").GetGuid();

    await using (var database = context.Factory.CreateContext())
    {
      await database.PrintJobs
                    .Where(job => job.StationOrderId == stationOrderId)
                    .ExecuteUpdateAsync(job => job.SetProperty(entry => entry.Status, PrintJobStatus.Printed));
      await database.SaveChangesAsync();
    }

    Task<HttpResponseMessage> firstTap = ReprintAsync(orderId, stationOrderId);
    Task<HttpResponseMessage> secondTap = ReprintAsync(orderId, stationOrderId);
    HttpResponseMessage[] responses = await Task.WhenAll(firstTap, secondTap);

    foreach (var response in responses)
    {
      response.Dispose();
    }

    await using var verification = context.Factory.CreateContext();
    Assert.That(await verification.PrintJobs.CountAsync(job => job.StationOrderId == stationOrderId && job.CopyNumber == 1),
                Is.EqualTo(1));
  }

  private Task<HttpResponseMessage> ReprintAsync(Guid orderId, Guid stationOrderId)
  {
    return context.SendAsync(HttpMethod.Post,
                             $"/api/orders/{orderId}/station-orders/{stationOrderId}/print-another-copy");
  }
}
