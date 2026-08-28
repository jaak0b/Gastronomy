using System.Text.Json;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class PrintJobEnsuredTest
{
  private OrderTestContext context = null!;

  [SetUp]
  public async Task SetUp()
  {
    context = await new OrderTestContext.Builder().StartAsync(withRunningPrinters: false);
  }

  [TearDown]
  public async Task TearDown()
  {
    await context.DisposeAsync();
  }

  [Test]
  public async Task PostOrder_RetriedAfterATicketWasNeverHandedToAPrinter_HandsItOverOnTheRetry()
  {
    Guid clientOrderId = Guid.NewGuid();
    using HttpResponseMessage first = await context.PostOrderAsync(context.BuildOrder(clientOrderId));
    JsonDocument body = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
    Guid stationOrderId = body.RootElement.GetProperty("stationOrders")[0].GetProperty("stationOrderId").GetGuid();

    await using (GastronomyAppDbContext database = context.Factory.CreateContext())
    {
      await database.PrintJobs
          .Where(job => job.StationOrderId == stationOrderId)
          .ExecuteDeleteAsync();
    }

    using HttpResponseMessage retry = await context.PostOrderAsync(context.BuildOrder(clientOrderId));

    await using GastronomyAppDbContext verification = context.Factory.CreateContext();
    Assert.That(
        await verification.PrintJobs.CountAsync(job => job.StationOrderId == stationOrderId),
        Is.EqualTo(1));
  }

  [Test]
  public async Task PostOrder_RetriedWhileTheSlipIsStillWaiting_DoesNotQueueASecondPrint()
  {
    Guid clientOrderId = Guid.NewGuid();
    using HttpResponseMessage first = await context.PostOrderAsync(context.BuildOrder(clientOrderId));
    JsonDocument body = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
    Guid stationOrderId = body.RootElement.GetProperty("stationOrders")[0].GetProperty("stationOrderId").GetGuid();

    using HttpResponseMessage retry = await context.PostOrderAsync(context.BuildOrder(clientOrderId));

    await using GastronomyAppDbContext verification = context.Factory.CreateContext();
    Assert.That(
        await verification.PrintJobs.CountAsync(job => job.StationOrderId == stationOrderId),
        Is.EqualTo(1));
  }

  [Test]
  public async Task PostReprint_TwoSimultaneousTaps_QueuesAtMostOneJob()
  {
    using HttpResponseMessage created = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid()));
    JsonDocument body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
    Guid orderId = body.RootElement.GetProperty("orderId").GetGuid();
    Guid stationOrderId = body.RootElement.GetProperty("stationOrders")[0].GetProperty("stationOrderId").GetGuid();

    await using (GastronomyAppDbContext database = context.Factory.CreateContext())
    {
      await database.PrintJobs
          .Where(job => job.StationOrderId == stationOrderId)
          .ExecuteUpdateAsync(job => job.SetProperty(entry => entry.Status, PrintJobStatus.Printed));
      await database.SaveChangesAsync();
    }

    Task<HttpResponseMessage> firstTap = ReprintAsync(orderId, stationOrderId);
    Task<HttpResponseMessage> secondTap = ReprintAsync(orderId, stationOrderId);
    HttpResponseMessage[] responses = await Task.WhenAll(firstTap, secondTap);

    foreach (HttpResponseMessage response in responses)
    {
      response.Dispose();
    }

    await using GastronomyAppDbContext verification = context.Factory.CreateContext();
    Assert.That(
        await verification.PrintJobs.CountAsync(
            job => job.StationOrderId == stationOrderId && job.CopyNumber == 1),
        Is.EqualTo(1));
  }

  private Task<HttpResponseMessage> ReprintAsync(Guid orderId, Guid stationOrderId)
  {
    return context.SendAsync(
        HttpMethod.Post,
        $"/api/orders/{orderId}/station-orders/{stationOrderId}/print-another-copy");
  }
}
