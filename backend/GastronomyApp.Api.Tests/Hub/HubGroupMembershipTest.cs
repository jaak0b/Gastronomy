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
    private string otherStaffMemberToken = null!;

    [SetUp]
    public async Task SetUp()
    {
        context = await new OrderTestContext.Builder().StartAsync(withRunningPrinters: false);

        Guid otherStaffMemberId = Guid.NewGuid();

        await using (GastronomyAppDbContext database = context.Factory.CreateContext())
        {
            database.StaffMembers.Add(new StaffMember
            {
                Id = otherStaffMemberId,
                Name = "Bea",
                IsActive = true,
                CreatedAtUtc = new DateTime(2026, 8, 26, 19, 40, 0, DateTimeKind.Utc),
            });

            await database.SaveChangesAsync();
        }

        using IServiceScope scope = context.Factory.Services.CreateScope();
        IssuedDeviceToken issued = await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
            .IssueAsync(otherStaffMemberId, "de", "NUnit", CancellationToken.None);
        otherStaffMemberToken = issued.PlaintextToken;
    }

    [TearDown]
    public async Task TearDown()
    {
        await context.DisposeAsync();
    }

    [Test]
    public async Task OrderAccepted_TwoStaffMembersConnected_ReachesOnlyThePlacingStaffMembersGroup()
    {
        TaskCompletionSource<Guid> placingStaffMemberHeard = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<Guid> otherStaffMemberHeard = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await using HubConnection placingStaffMember = Connect(context.DeviceToken);
        await using HubConnection otherStaffMember = Connect(otherStaffMemberToken);

        placingStaffMember.On<JsonElement>(
            "OrderAccepted",
            payload => placingStaffMemberHeard.TrySetResult(payload.GetProperty("orderId").GetGuid()));

        otherStaffMember.On<JsonElement>(
            "OrderAccepted",
            payload => otherStaffMemberHeard.TrySetResult(payload.GetProperty("orderId").GetGuid()));

        await placingStaffMember.StartAsync();
        await otherStaffMember.StartAsync();

        Guid orderId;

        using (HttpResponseMessage response = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid())))
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            orderId = body.RootElement.GetProperty("orderId").GetGuid();
        }

        Task heard = await Task.WhenAny(placingStaffMemberHeard.Task, Task.Delay(patience));

        Assert.That(heard, Is.SameAs(placingStaffMemberHeard.Task), "The staff member who placed the order never heard OrderAccepted.");
        Assert.That(await placingStaffMemberHeard.Task, Is.EqualTo(orderId));

        Task silence = await Task.WhenAny(otherStaffMemberHeard.Task, Task.Delay(silenceWindow));

        Assert.That(
            silence,
            Is.Not.SameAs(otherStaffMemberHeard.Task),
            "A second staff member received an order event that was not theirs.");
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
