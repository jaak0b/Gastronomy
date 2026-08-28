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
    context = await new OrderTestContext.Builder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await context.DisposeAsync();
  }

  private readonly TimeSpan patience = TimeSpan.FromSeconds(20);

  private OrderTestContext context = null!;

  [Test]
  public async Task PlaceOrder_RealFleetOverTheMockTransport_WritesASlipFileForEveryTicket()
  {
    using var response = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid()));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var slipAppeared = await WaitUntilAsync(() =>
                                              Directory.Exists(context.Factory.MockSlipFolder)
                                              && Directory.GetFiles(context.Factory.MockSlipFolder, "*", SearchOption.AllDirectories).Length > 0);

    Assert.That(slipAppeared, Is.True, "No mock slip file appeared for the accepted order.");
  }

  [Test]
  public async Task PlaceOrder_RealFleetOverTheTestPrinter_ReachesTheTicketPrintedState()
  {
    Guid ticketId;

    using (var response = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid())))
    {
      var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
      ticketId = body.RootElement.GetProperty("stationOrders")[0].GetProperty("stationOrderId").GetGuid();
    }

    var reachedTestPrinterState = await WaitUntilAsync(async () =>
                                                       {
                                                         await using var database = context.Factory.CreateContext();
                                                         return await database.PrintJobs.AnyAsync(job => job.StationOrderId == ticketId && job.Status == PrintJobStatus.Printed);
                                                       });

    Assert.That(reachedTestPrinterState, Is.True, "The ticket never reached Printed.");
  }

  [Test]
  public async Task PlaceOrder_ConnectedHubClientInThePlacingStaffMembersGroup_ReceivesPrintJobStatusChanged()
  {
    TaskCompletionSource<string> received = new(TaskCreationOptions.RunContinuationsAsynchronously);

    await using var connection = new HubConnectionBuilder()
                                .WithUrl(new Uri(context.Factory.BaseAddress, $"hub?access_token={context.DeviceToken}"))
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

    using var response = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid()));

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var completed = await Task.WhenAny(received.Task, Task.Delay(patience));

    Assert.That(completed, Is.SameAs(received.Task), "No PrintJobStatusChanged push reached the staff member who placed the order.");
    Assert.That(await received.Task,
                Is.EqualTo(PrintJobStatus.Printed.ToString()));
  }

  private async Task<bool> WaitUntilAsync(Func<bool> condition)
  {
    return await WaitUntilAsync(() => Task.FromResult(condition()));
  }

  private async Task<bool> WaitUntilAsync(Func<Task<bool>> condition)
  {
    var deadline = DateTime.UtcNow.Add(patience);

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
