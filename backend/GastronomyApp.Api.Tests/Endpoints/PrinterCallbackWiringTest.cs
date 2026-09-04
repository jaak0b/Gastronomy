using System.Net;
using System.Text.Json;
using GastronomyApp.Core.Enums;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class PrinterCallbackWiringTest
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

  private readonly TimeSpan _patience = TimeSpan.FromSeconds(20);

  private OrderTestContext _context = null!;

  [Test]
  public async Task PlaceOrder_RealFleetOverTheMockTransport_WritesASlipFileForEveryTicket()
  {
    using var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var slipAppeared = await WaitUntilAsync(() =>
                                              Directory.Exists(_context.Factory.MockSlipFolder)
                                              && Directory.GetFiles(_context.Factory.MockSlipFolder, "*", SearchOption.AllDirectories).Length > 0);

    Assert.That(slipAppeared, Is.True, "No mock slip file appeared for the accepted order.");
  }

  [Test]
  public async Task PlaceOrder_RealFleetOverTheTestPrinter_ReachesTheTicketPrintedState()
  {
    Guid ticketId;

    using (var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid())))
    {
      var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
      ticketId = body.RootElement.GetProperty("stationOrders")[0].GetProperty("stationOrderId").GetGuid();
    }

    var reachedTestPrinterState = await WaitUntilAsync(async () =>
                                                       {
                                                         await using var database = _context.Factory.CreateContext();
                                                         return await database.PrintJobs.AnyAsync(job => job.StationOrderId == ticketId && job.Status == PrintJobStatus.Printed);
                                                       });

    Assert.That(reachedTestPrinterState, Is.True, "The ticket never reached Printed.");
  }

  [Test]
  public async Task PlaceOrder_ConnectedPhone_ReceivesPrintJobStatusChanged()
  {
    TaskCompletionSource<string> received = new(TaskCreationOptions.RunContinuationsAsynchronously);

    await using var connection = new HubConnectionBuilder()
                                .WithUrl(new Uri(_context.Factory.BaseAddress, $"hub?access_token={_context.DeviceToken}"))
                                .Build();

    connection.On<JsonElement>("PrintJobStatusChanged",
                               payload =>
                               {
                                 var status = payload.GetProperty("status").GetString() ?? string.Empty;

                                 if (status == PrintJobStatus.Printed.ToString())
                                 {
                                   received.TrySetResult(status);
                                 }
                               });

    await connection.StartAsync();

    using var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var completed = await Task.WhenAny(received.Task, Task.Delay(_patience));

    Assert.That(completed, Is.SameAs(received.Task), "No PrintJobStatusChanged push reached the phone.");
    Assert.That(await received.Task,
                Is.EqualTo(PrintJobStatus.Printed.ToString()));
  }

  [Test]
  public async Task PlaceOrder_ConnectedLaptopAdministration_ReceivesOrderStatusChanged()
  {
    TaskCompletionSource<string> received = new(TaskCreationOptions.RunContinuationsAsynchronously);

    await using var connection = new HubConnectionBuilder()
                                .WithUrl(new Uri(_context.Factory.BaseAddress, "hub"))
                                .Build();

    connection.On<JsonElement>("OrderStatusChanged",
                               payload =>
                               {
                                 var status = payload.GetProperty("status").GetString() ?? string.Empty;

                                 if (status == OrderStatus.Printed.ToString())
                                 {
                                   received.TrySetResult(status);
                                 }
                               });

    await connection.StartAsync();

    using var response = await _context.PostOrderAsync(_context.BuildOrder(Guid.NewGuid()));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var completed = await Task.WhenAny(received.Task, Task.Delay(_patience));

    Assert.That(completed, Is.SameAs(received.Task), "No OrderStatusChanged push reached the laptop.");
    Assert.That(await received.Task,
                Is.EqualTo(OrderStatus.Printed.ToString()));
  }

  private async Task<bool> WaitUntilAsync(Func<bool> condition)
  {
    return await WaitUntilAsync(() => Task.FromResult(condition()));
  }

  private async Task<bool> WaitUntilAsync(Func<Task<bool>> condition)
  {
    var deadline = DateTime.UtcNow.Add(_patience);

    while (DateTime.UtcNow < deadline)
    {
      if (await condition())
      {
        return true;
      }

      await Task.Delay(100);
    }

    return false;
  }
}
