using System.Net;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class OrderIdempotencyTest
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
    public async Task PostOrder_SameSubmissionIdAndContent_ReturnsTheOriginalBodyByteForByte()
    {
        OrderBody body = context.BuildOrder(Guid.NewGuid(), 700);

        string firstBody;
        using (HttpResponseMessage first = await context.PostOrderAsync(body))
        {
            firstBody = await first.Content.ReadAsStringAsync();
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        string secondBody;
        using (HttpResponseMessage second = await context.PostOrderAsync(body))
        {
            secondBody = await second.Content.ReadAsStringAsync();
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        Assert.That(secondBody, Is.EqualTo(firstBody));

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        int orderCount = await database.Orders.CountAsync();
        int ticketCount = await database.LocationTickets.CountAsync();
        int printJobCount = await database.PrintJobs.CountAsync(job => job.Kind == PrintJobKind.Initial);

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
        Guid clientOrderId = Guid.NewGuid();

        using (HttpResponseMessage first = await context.PostOrderAsync(context.BuildOrder(clientOrderId, 700)))
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        OrderBody different = new(
            clientOrderId,
            "Tisch 99",
            null,
            700,
            [new OrderLineBody(context.World.BratwurstItemId, 2, null, null)]);

        using HttpResponseMessage second = await context.PostOrderAsync(different);

        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }
}
