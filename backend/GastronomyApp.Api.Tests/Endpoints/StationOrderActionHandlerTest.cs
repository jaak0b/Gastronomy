using System.Net;
using System.Text.Json;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class StationOrderActionHandlerTest
{

  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync(false);

    using var created = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));
    var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
    _orderId = body.RootElement.GetProperty("orderId").GetGuid();
    _stationOrderId = body.RootElement.GetProperty("stationOrders")[0].GetProperty("stationOrderId").GetGuid();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;
  private Guid _orderId;
  private Guid _stationOrderId;

  [Test]
  public async Task PostPrintAnotherCopy_WhileTheFirstSlipIsStillWaiting_NamesTheWordingTheLocalesDefine()
  {
    using var response = await PrintAnotherCopyAsync();
    var messageKey = await MessageKeyOfAsync(response);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(messageKey, Is.EqualTo("printJob.reprintNotAllowed"));
                    });
  }

  [Test]
  public async Task PostPrintAnotherCopy_WhileAnEarlierCopyIsStillWaiting_NamesTheWordingTheLocalesDefine()
  {
    await using (var database = _context.Factory.CreateContext())
    {
      database.PrintJobs.Add(new PrintJob
                             {
                               Id = Guid.NewGuid(),
                               StationOrderId = _stationOrderId,
                               CopyNumber = 1,
                               Status = PrintJobStatus.Printed,
                               CreatedAtUtc = DateTime.UtcNow
                             });
      await database.SaveChangesAsync();
    }

    await _context.Factory.ReconcilePrintersAsync();

    using var response = await PrintAnotherCopyAsync();
    var messageKey = await MessageKeyOfAsync(response);

    Assert.Multiple(() =>
                    {
                      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
                      Assert.That(messageKey, Is.EqualTo("printJob.printJobAlreadyRunning"));
                    });
  }

  private async Task<string> MessageKeyOfAsync(HttpResponseMessage response)
  {
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("messageKey").GetString()!;
  }

  private Task<HttpResponseMessage> PrintAnotherCopyAsync()
  {
    return _context.Client.PostAsync($"/api/admin/orders/{_orderId}/station-orders/{_stationOrderId}/print-another-copy",
                                    null);
  }
}
