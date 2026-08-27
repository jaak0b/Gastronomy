using System.Text.Json;
using GastronomyApp.Core.Entities;
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
        using HttpResponseMessage first = await context.PostOrderAsync(context.BuildOrder(clientOrderId, 700));
        JsonDocument body = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        Guid ticketId = body.RootElement.GetProperty("tickets")[0].GetProperty("ticketId").GetGuid();

        await using (GastronomyAppDbContext database = context.Factory.CreateContext())
        {
            await database.PrintJobs
                .Where(job => job.LocationTicketId == ticketId)
                .ExecuteDeleteAsync();
        }

        using HttpResponseMessage retry = await context.PostOrderAsync(context.BuildOrder(clientOrderId, 700));

        await using GastronomyAppDbContext verification = context.Factory.CreateContext();
        Assert.That(
            await verification.PrintJobs.CountAsync(job => job.LocationTicketId == ticketId),
            Is.EqualTo(1));
    }

    [Test]
    public async Task PostOrder_RetriedWhileTheSlipIsStillWaiting_DoesNotQueueASecondPrint()
    {
        Guid clientOrderId = Guid.NewGuid();
        using HttpResponseMessage first = await context.PostOrderAsync(context.BuildOrder(clientOrderId, 700));
        JsonDocument body = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        Guid ticketId = body.RootElement.GetProperty("tickets")[0].GetProperty("ticketId").GetGuid();

        using HttpResponseMessage retry = await context.PostOrderAsync(context.BuildOrder(clientOrderId, 700));

        await using GastronomyAppDbContext verification = context.Factory.CreateContext();
        Assert.That(
            await verification.PrintJobs.CountAsync(job => job.LocationTicketId == ticketId),
            Is.EqualTo(1));
    }

    [Test]
    public async Task PostReprint_TwoSimultaneousTaps_QueuesAtMostOneJob()
    {
        using HttpResponseMessage created = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700));
        JsonDocument body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Guid orderId = body.RootElement.GetProperty("orderId").GetGuid();
        Guid ticketId = body.RootElement.GetProperty("tickets")[0].GetProperty("ticketId").GetGuid();

        await using (GastronomyAppDbContext database = context.Factory.CreateContext())
        {
            LocationTicket ticket = await database.LocationTickets.FirstAsync(candidate => candidate.Id == ticketId);
            ticket.Status = LocationTicketStatus.Printed;
            await database.PrintJobs
                .Where(job => job.LocationTicketId == ticketId)
                .ExecuteUpdateAsync(job => job.SetProperty(entry => entry.Status, PrintJobStatus.Confirmed));
            await database.SaveChangesAsync();
        }

        Task<HttpResponseMessage> firstTap = ReprintAsync(orderId, ticketId);
        Task<HttpResponseMessage> secondTap = ReprintAsync(orderId, ticketId);
        HttpResponseMessage[] responses = await Task.WhenAll(firstTap, secondTap);

        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }

        await using GastronomyAppDbContext verification = context.Factory.CreateContext();
        Assert.That(
            await verification.PrintJobs.CountAsync(
                job => job.LocationTicketId == ticketId && job.Kind == PrintJobKind.Reprint),
            Is.EqualTo(1));
    }

    private Task<HttpResponseMessage> ReprintAsync(Guid orderId, Guid ticketId)
    {
        return context.SendAsync(
            HttpMethod.Post,
            $"/api/orders/{orderId}/tickets/{ticketId}/reprint");
    }
}
