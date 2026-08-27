using System.Net;
using System.Text.Json;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class PrinterCallbackWiringTest
{
    private readonly TimeSpan patience = TimeSpan.FromSeconds(20);

    private OrderTestContext context = null!;

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

    [Test]
    public async Task PlaceOrder_RealFleetOverTheMockTransport_WritesASlipFileForEveryTicket()
    {
        using HttpResponseMessage response = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        bool slipAppeared = await WaitUntilAsync(() =>
            Directory.Exists(context.Factory.MockSlipFolder)
            && Directory.GetFiles(context.Factory.MockSlipFolder, "*", SearchOption.AllDirectories).Length > 0);

        Assert.That(slipAppeared, Is.True, "No mock slip file appeared for the accepted order.");
    }

    [Test]
    public async Task PlaceOrder_RealFleetOverTheMockTransport_ReachesTheTicketPrintedOnTestPrinterState()
    {
        Guid ticketId;

        using (HttpResponseMessage response = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700)))
        {
            JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            ticketId = body.RootElement.GetProperty("tickets")[0].GetProperty("ticketId").GetGuid();
        }

        bool reachedTestPrinterState = await WaitUntilAsync(async () =>
        {
            await using GastronomyAppDbContext database = context.Factory.CreateContext();
            return await database.LocationTickets.AnyAsync(
                ticket => ticket.Id == ticketId && ticket.Status == LocationTicketStatus.PrintedOnTestPrinter);
        });

        Assert.That(reachedTestPrinterState, Is.True, "The ticket never reached PrintedOnTestPrinter.");
    }

    [Test]
    public async Task PlaceOrder_ConnectedHubClientInThePlacingStaffMembersGroup_ReceivesTicketStatusChanged()
    {
        TaskCompletionSource<string> received = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await using HubConnection connection = new HubConnectionBuilder()
            .WithUrl(new Uri(context.Factory.BaseAddress, $"hub?access_token={context.DeviceToken}"))
            .Build();

        connection.On<JsonElement>("TicketStatusChanged", payload =>
        {
            string status = payload.GetProperty("status").GetString() ?? string.Empty;

            if (status == LocationTicketStatus.PrintedOnTestPrinter.ToString())
            {
                received.TrySetResult(status);
            }
        });

        await connection.StartAsync();

        using HttpResponseMessage response = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        Task completed = await Task.WhenAny(received.Task, Task.Delay(patience));

        Assert.That(completed, Is.SameAs(received.Task), "No TicketStatusChanged push reached the staff member who placed the order.");
        Assert.That(
            await received.Task,
            Is.EqualTo(LocationTicketStatus.PrintedOnTestPrinter.ToString()));
    }

    private async Task<bool> WaitUntilAsync(Func<bool> condition)
    {
        return await WaitUntilAsync(() => Task.FromResult(condition()));
    }

    private async Task<bool> WaitUntilAsync(Func<Task<bool>> condition)
    {
        DateTime deadline = DateTime.UtcNow.Add(patience);

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
