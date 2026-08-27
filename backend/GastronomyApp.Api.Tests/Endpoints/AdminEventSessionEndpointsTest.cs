using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class AdminEventSessionEndpointsTest
{
    private readonly EventSessionStartGuardNames guardNames = new();

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
    public async Task PostEventSession_PracticeRunOnACleanSession_StartsItAndEndsThePrevious()
    {
        using HttpResponseMessage response = await StartSessionAsync("Probelauf", isPractice: true, confirmedName: null);
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        Guid startedSessionId = body.RootElement.GetProperty("eventSession").GetProperty("id").GetGuid();

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        EventSession started = await database.EventSessions.FirstAsync(
            session => session.Id == startedSessionId);
        EventSession previous = await database.EventSessions.FirstAsync(
            session => session.Id == context.World.EventSessionId);

        Assert.Multiple(() =>
        {
            Assert.That(started.IsActive, Is.True);
            Assert.That(started.IsPractice, Is.True);
            Assert.That(started.Name, Is.EqualTo("Probelauf"));
            Assert.That(previous.IsActive, Is.False, "The previous session must be ended in the same transaction.");
            Assert.That(previous.EndedAtUtc, Is.Not.Null);
        });
    }

    [Test]
    public async Task PostEventSession_NewSessionStarted_ResetsTheOrderNumbering()
    {
        using (HttpResponseMessage firstOrder = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700)))
        {
            JsonDocument body = JsonDocument.Parse(await firstOrder.Content.ReadAsStringAsync());

            Assert.That(body.RootElement.GetProperty("globalOrderNumber").GetInt32(), Is.EqualTo(1));
        }

        await SettleEveryTicketAsync();

        using (HttpResponseMessage started = await StartSessionAsync(
            "Probelauf",
            isPractice: true,
            confirmedName: "Probelauf"))
        {
            Assert.That(started.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        using HttpResponseMessage secondOrder = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700));
        JsonDocument secondBody = JsonDocument.Parse(await secondOrder.Content.ReadAsStringAsync());

        Assert.That(secondBody.RootElement.GetProperty("globalOrderNumber").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task PostEventSession_LiveRunWhileStationsAreOnTheTestPrinter_IsRefusedNamingThatGuard()
    {
        using HttpResponseMessage response = await StartSessionAsync(
            "Samstagabend zwei",
            isPractice: false,
            confirmedName: null);

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        IReadOnlyList<string> guards = ReadGuards(body);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("EventSessionStartRefused"));
            Assert.That(guards, Does.Contain(guardNames.ActiveLocationOnTestPrinter));
        });

        JsonElement condition = body.RootElement.GetProperty("blockingConditions")
            .EnumerateArray()
            .First(entry => entry.GetProperty("guard").GetString()
                == guardNames.ActiveLocationOnTestPrinter);

        Assert.That(
            condition.GetProperty("parameters").GetProperty("stations").GetString(),
            Does.Contain("Kueche"));
    }

    [Test]
    public async Task PostEventSession_OpenTicketsAndARecentOrder_ListsEveryViolatedGuard()
    {
        using (HttpResponseMessage placed = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700)))
        {
            Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        await SetEveryTicketStatusAsync(LocationTicketStatus.Unknown);

        using HttpResponseMessage response = await StartSessionAsync(
            "Probelauf",
            isPractice: true,
            confirmedName: null);

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        IReadOnlyList<string> guards = ReadGuards(body);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(guards, Does.Contain(guardNames.NonFinalTicketsRemain));
            Assert.That(guards, Does.Contain(guardNames.UnansweredUnknownQuestionsRemain));
            Assert.That(guards, Does.Contain(guardNames.RecentOrderNeedsTypedConfirmation));
        });
    }

    [Test]
    public async Task PostEventSession_RecentOrderWithoutTheTypedName_IsRefused()
    {
        using (HttpResponseMessage placed = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700)))
        {
            Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        await SettleEveryTicketAsync();

        using HttpResponseMessage response = await StartSessionAsync(
            "Probelauf",
            isPractice: true,
            confirmedName: null);

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(
                ReadGuards(body),
                Does.Contain(guardNames.RecentOrderNeedsTypedConfirmation));
        });
    }

    [Test]
    public async Task PostEventSession_RecentOrderWithTheTypedNameConfirmed_IsAllowed()
    {
        using (HttpResponseMessage placed = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700)))
        {
            Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        await SettleEveryTicketAsync();

        using HttpResponseMessage response = await StartSessionAsync(
            "Probelauf",
            isPractice: true,
            confirmedName: "Probelauf");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task GetEventSession_StationsOnTheTestPrinter_ReportsWhatBlocksStarting()
    {
        using HttpResponseMessage response = await context.Client.GetAsync("/api/admin/event-session");
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(
                body.RootElement.GetProperty("eventSession").GetProperty("name").GetString(),
                Is.EqualTo("Samstagabend"));
            Assert.That(
                body.RootElement.GetProperty("blocksStarting").GetProperty("violatedGuards").GetArrayLength(),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task GetEventSession_ProbeForWhatBlocksStarting_NeverEndsTheRunningSession()
    {
        using (HttpResponseMessage probe = await context.Client.GetAsync("/api/admin/event-session"))
        {
            Assert.That(probe.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        EventSession session = await database.EventSessions.FirstAsync(
            candidate => candidate.Id == context.World.EventSessionId);

        Assert.Multiple(() =>
        {
            Assert.That(session.IsActive, Is.True, "Reading what blocks a start must never end the running session.");
            Assert.That(session.EndedAtUtc, Is.Null);
        });
    }

    private Task<HttpResponseMessage> StartSessionAsync(string name, bool isPractice, string? confirmedName)
    {
        return context.Client.PostAsJsonAsync(
            "/api/admin/event-session",
            new StartSessionBody(name, isPractice, confirmedName));
    }

    private IReadOnlyList<string> ReadGuards(JsonDocument body)
    {
        return
        [
            .. body.RootElement.GetProperty("blockingConditions")
                .EnumerateArray()
                .Select(entry => entry.GetProperty("guard").GetString() ?? string.Empty),
        ];
    }

    private async Task SettleEveryTicketAsync()
    {
        await SetEveryTicketStatusAsync(LocationTicketStatus.HandledOnPaper);
    }

    private async Task SetEveryTicketStatusAsync(LocationTicketStatus status)
    {
        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        List<LocationTicket> tickets = await database.LocationTickets.ToListAsync();

        foreach (LocationTicket ticket in tickets)
        {
            ticket.Status = status;
        }

        await database.SaveChangesAsync();
    }
}

public sealed record StartSessionBody(string Name, bool IsPractice, string? ConfirmedName);

public sealed record EventSessionStartGuardNames
{
    public string NonFinalTicketsRemain { get; } = "NonFinalTicketsRemain";
    public string UnansweredUnknownQuestionsRemain { get; } = "UnansweredUnknownQuestionsRemain";
    public string RecentOrderNeedsTypedConfirmation { get; } = "RecentOrderNeedsTypedConfirmation";
    public string ActiveLocationOnTestPrinter { get; } = "ActiveLocationOnTestPrinter";
}
