using System.Net;
using System.Text.Json;
using GastronomyApp.Api.Tests.Endpoints;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Hub;

[TestFixture]
public sealed class HubGroupMembershipTest
{
    private readonly TimeSpan patience = TimeSpan.FromSeconds(10);
    private readonly TimeSpan silenceWindow = TimeSpan.FromSeconds(2);

    private OrderTestContext context = null!;
    private string otherPersonToken = null!;

    [SetUp]
    public async Task SetUp()
    {
        context = await new OrderTestContext.Builder().StartAsync(withRunningPrinters: false);

        Guid otherPersonId = Guid.NewGuid();

        await using (GastronomyAppDbContext database = context.Factory.CreateContext())
        {
            database.ServerPeople.Add(new ServerPerson
            {
                Id = otherPersonId,
                Name = "Bea",
                IsActive = true,
                CreatedAtUtc = new DateTime(2026, 8, 26, 19, 40, 0, DateTimeKind.Utc),
            });

            await database.SaveChangesAsync();
        }

        using IServiceScope scope = context.Factory.Services.CreateScope();
        IssuedDeviceToken issued = await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
            .IssueAsync(otherPersonId, "de", "NUnit", CancellationToken.None);
        otherPersonToken = issued.PlaintextToken;
    }

    [TearDown]
    public async Task TearDown()
    {
        await context.DisposeAsync();
    }

    [Test]
    public async Task OrderAccepted_TwoPeopleConnected_ReachesOnlyThePlacingPersonsGroup()
    {
        TaskCompletionSource<Guid> placingPersonHeard = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<Guid> otherPersonHeard = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await using HubConnection placingPerson = Connect(context.DeviceToken);
        await using HubConnection otherPerson = Connect(otherPersonToken);

        placingPerson.On<JsonElement>(
            "OrderAccepted",
            payload => placingPersonHeard.TrySetResult(payload.GetProperty("orderId").GetGuid()));

        otherPerson.On<JsonElement>(
            "OrderAccepted",
            payload => otherPersonHeard.TrySetResult(payload.GetProperty("orderId").GetGuid()));

        await placingPerson.StartAsync();
        await otherPerson.StartAsync();

        Guid orderId;

        using (HttpResponseMessage response = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid(), 700)))
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            orderId = body.RootElement.GetProperty("orderId").GetGuid();
        }

        Task heard = await Task.WhenAny(placingPersonHeard.Task, Task.Delay(patience));

        Assert.That(heard, Is.SameAs(placingPersonHeard.Task), "The placing person never heard OrderAccepted.");
        Assert.That(await placingPersonHeard.Task, Is.EqualTo(orderId));

        Task silence = await Task.WhenAny(otherPersonHeard.Task, Task.Delay(silenceWindow));

        Assert.That(
            silence,
            Is.Not.SameAs(otherPersonHeard.Task),
            "A second person received an order event that was not theirs.");
    }

    [Test]
    public async Task Connect_ValidDeviceTokenAsAccessTokenQuery_IsAccepted()
    {
        await using HubConnection connection = Connect(context.DeviceToken);

        await connection.StartAsync();

        Assert.That(connection.State, Is.EqualTo(HubConnectionState.Connected));
    }

    private HubConnection Connect(string deviceToken)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(context.Factory.BaseAddress, $"hub?access_token={deviceToken}"))
            .Build();
    }
}
