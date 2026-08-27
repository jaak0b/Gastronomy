using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class ResolveRaceTest
{
    private OrderTestContext context = null!;
    private Guid orderId;
    private Guid ticketId;

    [SetUp]
    public async Task SetUp()
    {
        context = await new OrderTestContext.Builder().StartAsync(withRunningPrinters: false);

        using HttpResponseMessage created = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700));
        JsonDocument body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        orderId = body.RootElement.GetProperty("orderId").GetGuid();
        ticketId = body.RootElement.GetProperty("tickets")[0].GetProperty("ticketId").GetGuid();

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        LocationTicket ticket = await database.LocationTickets.FirstAsync(candidate => candidate.Id == ticketId);
        ticket.Status = LocationTicketStatus.Unknown;
        await database.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await context.DisposeAsync();
    }

    [Test]
    public async Task PostResolve_TwoSimultaneousAnswers_AcceptsOneAndNeverReprintsTwice()
    {
        Task<HttpResponseMessage> first = ResolveAsync();
        Task<HttpResponseMessage> second = ResolveAsync();

        HttpResponseMessage[] responses = await Task.WhenAll(first, second);

        int accepted = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
        int refused = responses.Count(response => response.StatusCode == HttpStatusCode.Conflict);

        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        int reprintJobs = await database.PrintJobs.CountAsync(job => job.Kind == PrintJobKind.Reprint);

        Assert.Multiple(() =>
        {
            Assert.That(accepted, Is.EqualTo(1), "Exactly one answer to the question may win.");
            Assert.That(refused, Is.EqualTo(1), "The losing answer must be told the question was already answered.");
            Assert.That(reprintJobs, Is.EqualTo(1), "A ticket must never be reprinted twice for one question.");
        });
    }

    private Task<HttpResponseMessage> ResolveAsync()
    {
        return context.Client.SendAsync(BuildRequest());
    }

    private HttpRequestMessage BuildRequest()
    {
        HttpRequestMessage request = new(
            HttpMethod.Post,
            $"/api/orders/{orderId}/tickets/{ticketId}/resolve");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.DeviceToken);
        request.Content = JsonContent.Create(new SlipOnThePileBody(false));

        return request;
    }
}
