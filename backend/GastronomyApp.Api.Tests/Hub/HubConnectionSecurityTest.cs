using System.Net;
using System.Text.Json;
using GastronomyApp.Api.Tests.Endpoints;
using Microsoft.AspNetCore.SignalR.Client;

namespace GastronomyApp.Api.Tests.Hub;

[TestFixture]
public sealed class HubConnectionSecurityTest
{
    private readonly TimeSpan patience = TimeSpan.FromSeconds(10);
    private readonly TimeSpan silenceWindow = TimeSpan.FromSeconds(2);

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
    public async Task Connect_ValidStationAccessKey_JoinsTheSiteWideStationGroup()
    {
        TaskCompletionSource<Guid> heard = new(TaskCreationOptions.RunContinuationsAsynchronously);
        string accessKey = context.World.KitchenStationId.ToString("N");

        await using HubConnection connection = Connect($"hub?stationAccessKey={accessKey}");
        connection.On<JsonElement>(
            "OrderAccepted",
            payload => heard.TrySetResult(payload.GetProperty("orderId").GetGuid()));

        await connection.StartAsync();

        Guid orderId = await PlaceAnOrderAsync();
        Task received = await Task.WhenAny(heard.Task, Task.Delay(patience));

        Assert.Multiple(() =>
        {
            Assert.That(received, Is.SameAs(heard.Task), "A valid station key must join the stations group.");
            Assert.That(connection.State, Is.EqualTo(HubConnectionState.Connected));
        });

        Assert.That(await heard.Task, Is.EqualTo(orderId));
    }

    [Test]
    public async Task Deactivate_ConnectedPhone_IsRemovedFromEveryGroupAndClosed()
    {
        TaskCompletionSource<Guid> heardBeforeRevocation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<Guid> heardAfterRevocation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool revoked = false;

        await using HubConnection connection = Connect($"hub?access_token={context.DeviceToken}");
        connection.On<JsonElement>("OrderAccepted", payload =>
        {
            Guid orderId = payload.GetProperty("orderId").GetGuid();

            if (revoked)
            {
                heardAfterRevocation.TrySetResult(orderId);
                return;
            }

            heardBeforeRevocation.TrySetResult(orderId);
        });

        await connection.StartAsync();
        await PlaceAnOrderAsync();

        Task beforeRevocation = await Task.WhenAny(heardBeforeRevocation.Task, Task.Delay(patience));

        Assert.That(
            beforeRevocation,
            Is.SameAs(heardBeforeRevocation.Task),
            "The phone must receive its own order events before it is revoked.");

        revoked = true;

        using (HttpResponseMessage revocation = await context.Client.PostAsync(
            $"/api/admin/staff-members/{context.World.StaffMemberId}/deactivate",
            content: null))
        {
            Assert.That(revocation.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        bool closed = await WaitUntilAsync(() => connection.State != HubConnectionState.Connected);

        Assert.That(closed, Is.True, "Revoking a device must abort its live connection.");

        using HttpResponseMessage afterRevocation = await context.SendAsync(HttpMethod.Get, "/api/session");

        Assert.That(afterRevocation.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private async Task<Guid> PlaceAnOrderAsync()
    {
        using HttpResponseMessage response = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid()));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return body.RootElement.GetProperty("orderId").GetGuid();
    }

    private async Task StartIgnoringRefusalAsync(HubConnection connection)
    {
        try
        {
            await connection.StartAsync();
        }
        catch (Exception exception) when (exception is not NUnit.Framework.AssertionException)
        {
            TestContext.Out.WriteLine($"The refused connection reported: {exception.Message}");
        }
    }

    private async Task<bool> WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.Add(patience);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(100);
        }

        return condition();
    }

    private HubConnection Connect(string relativeUrl)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(context.Factory.BaseAddress, relativeUrl))
            .Build();
    }
}
