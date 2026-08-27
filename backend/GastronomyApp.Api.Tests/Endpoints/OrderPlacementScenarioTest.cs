using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class OrderPlacementScenarioTest
{
    private readonly TimeSpan patience = TimeSpan.FromSeconds(20);

    private ApiTestFactory factory = null!;
    private SeededWorld world = null!;

    [SetUp]
    public async Task SetUp()
    {
        factory = await new ApiTestFactory.Builder().StartAsync();

        await using (GastronomyAppDbContext context = factory.CreateContext())
        {
            world = await new ApiSeeder().SeedAsync(context, CancellationToken.None);
        }

        await factory.ReconcilePrintersAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await factory.DisposeAsync();
    }

    [Test]
    public async Task OrderPlacementFlow_EnrolStartPracticeFetchCatalogAndSend_PrintsASlipPerStationAndTellsThePhone()
    {
        string deviceToken = await EnrolAPhoneAsync();

        CatalogSelection selection = await FetchCatalogAsync(deviceToken);

        PlacedOrder placed = await SendOrderAsync(deviceToken, selection);

        Assert.Multiple(() =>
        {
            Assert.That(placed.GlobalOrderNumber, Is.EqualTo(1), "A fresh session numbers from one.");
            Assert.That(placed.TicketCount, Is.EqualTo(2), "The order spans the kitchen and the bar.");
            Assert.That(
                placed.SequenceNumbers,
                Is.EqualTo(new[] { 1, 1 }),
                "Each station keeps its own independent run of sequence numbers.");
            Assert.That(placed.TotalCents, Is.EqualTo(1000));
        });

        bool bothSlipsWritten = await WaitUntilAsync(() =>
            Directory.Exists(factory.MockSlipFolder)
            && Directory.GetFiles(factory.MockSlipFolder, "*", SearchOption.AllDirectories).Length >= 2);

        Assert.That(bothSlipsWritten, Is.True, "One slip file per station must appear in the mock folder.");

        bool phoneSeesBothPrinted = await WaitUntilAsync(async () =>
        {
            IReadOnlyList<string> statuses = await ReadMyTicketStatusesAsync(deviceToken);

            return statuses.Count == 2
                && statuses.All(status => status == LocationTicketStatus.PrintedOnTestPrinter.ToString());
        });

        Assert.That(phoneSeesBothPrinted, Is.True, "The print state must come back to the phone's own endpoints.");

        string orderStatus = await ReadMyOrderStatusAsync(deviceToken);

        Assert.That(
            orderStatus,
            Is.EqualTo(OrderStatus.Printed.ToString()),
            "Inside a practice session the test printer is the expected transport, so the order reads as printed.");
    }

    private async Task<string> EnrolAPhoneAsync()
    {
        string qrCodeValue;

        using (HttpResponseMessage invitation = await factory.Client.PostAsJsonAsync(
            "/api/admin/enrolment/invitations",
            new { serverPersonId = (Guid?)null }))
        {
            Assert.That(invitation.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            JsonDocument body = JsonDocument.Parse(await invitation.Content.ReadAsStringAsync());
            string qrUrl = body.RootElement.GetProperty("qrUrl").GetString()!;
            qrCodeValue = qrUrl[(qrUrl.LastIndexOf('/') + 1)..];
        }

        using HttpResponseMessage redeemed = await factory.Client.PostAsJsonAsync(
            "/api/enrolment/redeem",
            new RedeemBody(qrCodeValue, null, "Anna", "NUnit"));

        Assert.That(redeemed.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        JsonDocument redemption = JsonDocument.Parse(await redeemed.Content.ReadAsStringAsync());

        return redemption.RootElement.GetProperty("deviceToken").GetString()!;
    }

    private async Task<CatalogSelection> FetchCatalogAsync(string deviceToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, "/api/catalog", deviceToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement items = body.RootElement.GetProperty("items");

        JsonElement bratwurst = items.EnumerateArray()
            .First(item => item.GetProperty("id").GetGuid() == world.BratwurstItemId);
        JsonElement beer = items.EnumerateArray()
            .First(item => item.GetProperty("id").GetGuid() == world.BeerItemId);

        Assert.Multiple(() =>
        {
            Assert.That(bratwurst.GetProperty("locationIds").GetArrayLength(), Is.EqualTo(1));
            Assert.That(beer.GetProperty("locationIds").GetArrayLength(), Is.EqualTo(1));
        });

        return new CatalogSelection(
            world.BratwurstItemId,
            bratwurst.GetProperty("priceCents").GetInt32(),
            world.BeerItemId,
            beer.GetProperty("priceCents").GetInt32());
    }

    private async Task<PlacedOrder> SendOrderAsync(string deviceToken, CatalogSelection selection)
    {
        int expectedTotalCents = (selection.BratwurstPriceCents * 2) + selection.BeerPriceCents;

        OrderBody body = new(
            Guid.NewGuid(),
            "Tisch 12",
            null,
            expectedTotalCents,
            [
                new OrderLineBody(selection.BratwurstItemId, 2, null, null),
                new OrderLineBody(selection.BeerItemId, 1, null, null),
            ]);

        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, "/api/orders", deviceToken, body);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        JsonDocument placed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement tickets = placed.RootElement.GetProperty("tickets");

        return new PlacedOrder(
            placed.RootElement.GetProperty("orderId").GetGuid(),
            placed.RootElement.GetProperty("globalOrderNumber").GetInt32(),
            placed.RootElement.GetProperty("totalCents").GetInt32(),
            tickets.GetArrayLength(),
            [.. tickets.EnumerateArray().Select(ticket => ticket.GetProperty("sequenceNumber").GetInt32())]);
    }

    private async Task<IReadOnlyList<string>> ReadMyTicketStatusesAsync(string deviceToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, "/api/orders/mine", deviceToken);
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement orders = body.RootElement.GetProperty("orders");

        if (orders.GetArrayLength() == 0)
        {
            return [];
        }

        return
        [
            .. orders[0].GetProperty("tickets")
                .EnumerateArray()
                .Select(ticket => ticket.GetProperty("status").GetString() ?? string.Empty),
        ];
    }

    private async Task<string> ReadMyOrderStatusAsync(string deviceToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, "/api/orders/mine", deviceToken);
        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return body.RootElement.GetProperty("orders")[0].GetProperty("status").GetString() ?? string.Empty;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string deviceToken,
        object? body = null)
    {
        using HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        return await factory.Client.SendAsync(request);
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

public sealed record CatalogSelection(
    Guid BratwurstItemId,
    int BratwurstPriceCents,
    Guid BeerItemId,
    int BeerPriceCents);

public sealed record PlacedOrder(
    Guid OrderId,
    int GlobalOrderNumber,
    int TotalCents,
    int TicketCount,
    IReadOnlyList<int> SequenceNumbers);
