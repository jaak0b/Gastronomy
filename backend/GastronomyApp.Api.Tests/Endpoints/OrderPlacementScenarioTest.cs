using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class OrderPlacementScenarioTest
{

  [SetUp]
  public async Task SetUp()
  {
    _factory = await new ApiTestFactory.Builder().StartAsync();

    await using (var context = _factory.CreateContext())
    {
      _world = await new ApiSeeder().SeedAsync(context, CancellationToken.None);
    }

    await _factory.ReconcilePrintersAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _factory.DisposeAsync();
  }

  private readonly TimeSpan _patience = TimeSpan.FromSeconds(20);

  private ApiTestFactory _factory = null!;
  private SeededWorld _world = null!;

  [Test]
  public async Task OrderPlacementFlow_EnrolStartPracticeFetchCatalogAndSend_PrintsASlipPerStation()
  {
    var deviceToken = await EnrolAPhoneAsync();

    var selection = await FetchCatalogAsync(deviceToken);

    var placed = await SendOrderAsync(deviceToken, selection);

    Assert.Multiple(() =>
                    {
                      Assert.That(placed.GlobalOrderNumber, Is.EqualTo(1), "A fresh session numbers from one.");
                      Assert.That(placed.TicketCount, Is.EqualTo(2), "The order spans the kitchen and the bar.");
                      Assert.That(placed.SequenceNumbers,
                                  Is.EqualTo(new[] { 1, 1 }),
                                  "Each station keeps its own independent run of sequence numbers.");
                      Assert.That(placed.TotalCents, Is.EqualTo(1000));
                    });

    var bothSlipsWritten = await WaitUntilAsync(() =>
                                                  Directory.Exists(_factory.MockSlipFolder)
                                                  && Directory.GetFiles(_factory.MockSlipFolder, "*", SearchOption.AllDirectories).Length >= 2);

    Assert.That(bothSlipsWritten, Is.True, "One slip file per station must appear in the mock folder.");

    var bothSlipsRead = await WaitUntilAsync(async () =>
                                            {
                                              IReadOnlyList<string> statuses = await ReadTicketStatusesAsync();

                                              return statuses.Count == 2
                                                     && statuses.All(status => status == PrintJobStatus.Printed.ToString());
                                            });

    Assert.That(bothSlipsRead, Is.True, "The print state of every slip must be readable once the printers have run.");

    var orderStatus = await ReadOrderStatusAsync();

    Assert.That(orderStatus,
                Is.EqualTo(OrderStatus.Printed.ToString()),
                "Inside a practice session the test printer is the expected transport, so the order reads as printed.");
  }

  private async Task<string> EnrolAPhoneAsync()
  {
    string qrCodeValue;

    using (var invitation = await _factory.Client.PostAsJsonAsync("/api/admin/enrolment/invitations",
                                                                 new { staffMemberId = (Guid?)null }))
    {
      Assert.That(invitation.StatusCode, Is.EqualTo(HttpStatusCode.Created));
      var body = JsonDocument.Parse(await invitation.Content.ReadAsStringAsync());
      var qrUrl = body.RootElement.GetProperty("qrUrl").GetString()!;
      qrCodeValue = qrUrl[(qrUrl.LastIndexOf('/') + 1)..];
    }

    using var redeemed = await _factory.Client.PostAsJsonAsync("/api/enrolment/redeem",
                                                              new RedeemBody(qrCodeValue, "Anna", "NUnit"));

    Assert.That(redeemed.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var redemption = JsonDocument.Parse(await redeemed.Content.ReadAsStringAsync());

    return redemption.RootElement.GetProperty("deviceToken").GetString()!;
  }

  private async Task<CatalogSelection> FetchCatalogAsync(string deviceToken)
  {
    using var response = await SendAsync(HttpMethod.Get, "/api/catalog", deviceToken);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var items = body.RootElement.GetProperty("items");

    var bratwurst = items.EnumerateArray()
                         .First(item => item.GetProperty("id").GetGuid() == _world.BratwurstItemId);
    var beer = items.EnumerateArray()
                    .First(item => item.GetProperty("id").GetGuid() == _world.BeerItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(bratwurst.GetProperty("stationIds").GetArrayLength(), Is.EqualTo(1));
                      Assert.That(beer.GetProperty("stationIds").GetArrayLength(), Is.EqualTo(1));
                    });

    return new(_world.BratwurstItemId,
               bratwurst.GetProperty("priceCents").GetInt32(),
               _world.BeerItemId,
               beer.GetProperty("priceCents").GetInt32());
  }

  private async Task<PlacedOrder> SendOrderAsync(string deviceToken, CatalogSelection selection)
  {
    OrderBody body = new(Guid.NewGuid(),
                         "Tisch 12",
                         null,
                         [
                           new(selection.BratwurstItemId, selection.BratwurstPriceCents, null, null), new(selection.BratwurstItemId, selection.BratwurstPriceCents, null, null),
                           new(selection.BeerItemId, selection.BeerPriceCents, null, null)
                         ]);

    using var response = await SendAsync(HttpMethod.Post, "/api/orders", deviceToken, body);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var placed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var tickets = placed.RootElement.GetProperty("stationOrders");

    return new(placed.RootElement.GetProperty("orderId").GetGuid(),
               placed.RootElement.GetProperty("globalOrderNumber").GetInt32(),
               placed.RootElement.GetProperty("totalCents").GetInt32(),
               tickets.GetArrayLength(),
               [.. tickets.EnumerateArray().Select(ticket => ticket.GetProperty("stationOrderNumber").GetInt32())]);
  }

  private async Task<IReadOnlyList<string>> ReadTicketStatusesAsync()
  {
    using var response = await _factory.Client.GetAsync("/api/admin/orders");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var orders = body.RootElement.GetProperty("orders");

    if (orders.GetArrayLength() == 0)
    {
      return [];
    }

    return
    [
      .. orders[0]
        .GetProperty("stationOrders")
        .EnumerateArray()
        .Select(ticket => ticket.GetProperty("status").GetString() ?? string.Empty)
    ];
  }

  private async Task<string> ReadOrderStatusAsync()
  {
    using var response = await _factory.Client.GetAsync("/api/admin/orders");
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.GetProperty("orders")[0].GetProperty("status").GetString() ?? string.Empty;
  }

  private async Task<HttpResponseMessage> SendAsync(HttpMethod method,
                                                    string path,
                                                    string deviceToken,
                                                    object? body = null)
  {
    using HttpRequestMessage request = new(method, path);
    request.Headers.Authorization = new("Bearer", deviceToken);

    if (body is not null)
    {
      request.Content = JsonContent.Create(body, body.GetType());
    }

    return await _factory.Client.SendAsync(request);
  }

  private async Task<bool> WaitUntilAsync(Func<bool> condition)
  {
    return await WaitUntilAsync(() => Task.FromResult(condition()));
  }

  private async Task<bool> WaitUntilAsync(Func<Task<bool>> condition)
  {
    var deadline = DateTime.UtcNow.Add(_patience);

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
