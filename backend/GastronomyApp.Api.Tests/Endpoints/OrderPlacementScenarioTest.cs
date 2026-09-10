using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class OrderPlacementScenarioTest
{

  [SetUp]
  public async Task SetUp()
  {
    _factory = await new ApiTestFactory.Builder().StartAsync();

    await using var context = _factory.CreateContext();
    _world = await new ApiSeeder().SeedAsync(context, CancellationToken.None);
  }

  [TearDown]
  public async Task TearDown()
  {
    await _factory.DisposeAsync();
  }

  private ApiTestFactory _factory = null!;
  private SeededWorld _world = null!;

  [Test]
  public async Task OrderPlacementFlow_EnrolFetchTheCatalogAndSend_SplitsTheOrderPerStation()
  {
    var deviceToken = await EnrolAPhoneAsync();

    var selection = await FetchCatalogAsync(deviceToken);

    var placed = await SendOrderAsync(deviceToken, selection);

    Assert.Multiple(() =>
                    {
                      Assert.That(placed.GlobalOrderNumber, Is.EqualTo(1), "A fresh session numbers from one.");
                      Assert.That(placed.StationOrderCount, Is.EqualTo(2), "The order spans the kitchen and the bar.");
                      Assert.That(placed.SequenceNumbers,
                                  Is.EqualTo(new[] { 1, 1 }),
                                  "Each station keeps its own independent run of sequence numbers.");
                      Assert.That(placed.TotalCents, Is.EqualTo(1000));
                    });

    await using var database = _factory.CreateContext();
    List<OrderItemStatusChange> changes = await database.OrderItemStatusChanges.ToListAsync();

    Assert.That(changes,
                Has.Count.EqualTo(3),
                "Every placed item is logged as waiting from the moment the order lands.");
  }

  [Test]
  public async Task OrderPlacementFlow_TwoStationsWithMixedDeliveryModes_ReachesTheTabletsAndTheOpenItemsList()
  {
    var deviceToken = await EnrolAPhoneAsync();
    var kitchenToken = await EnrolAStationTabletAsync(_world.KitchenStationId);

    OrderWithDeliveryModesBody order = new(Guid.NewGuid(),
                                           "Tisch 3",
                                           "Ohne Senf",
                                           [
                                             new(_world.BratwurstItemId, 350, null, null),
                                             new(_world.BratwurstItemId, 350, null, null),
                                             new(_world.BeerItemId, 300, null, null)
                                           ],
                                           [new(_world.BarStationId, "asItComes")]);

    using (var placed = await SendAsync(HttpMethod.Post, "/api/orders", deviceToken, order))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    IReadOnlyList<Guid> kitchenItemIds;

    using (var queue = await SendAsync(HttpMethod.Get, "/api/station/orders", kitchenToken))
    {
      Assert.That(queue.StatusCode, Is.EqualTo(HttpStatusCode.OK));

      var body = JsonDocument.Parse(await queue.Content.ReadAsStringAsync());
      var slice = body.RootElement.GetProperty("slices")[0];

      kitchenItemIds =
      [
        .. slice.GetProperty("items")
                .EnumerateArray()
                .Select(item => item.GetProperty("orderItemId").GetGuid())
      ];

      Assert.Multiple(() =>
                      {
                        Assert.That(body.RootElement.GetProperty("slices").GetArrayLength(), Is.EqualTo(1));
                        Assert.That(slice.GetProperty("tableName").GetString(), Is.EqualTo("Tisch 3"));
                        Assert.That(slice.GetProperty("deliveryMode").GetString(), Is.EqualTo("together"));
                        Assert.That(kitchenItemIds, Has.Count.EqualTo(2));
                      });
    }

    using (var advanced = await SendAsync(HttpMethod.Post,
                                          "/api/station/items/status",
                                          kitchenToken,
                                          new StationItemStatusBody(kitchenItemIds, "finished")))
    {
      Assert.That(advanced.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    await using (var database = _factory.CreateContext())
    {
      List<OrderItemStatusChange> changes = await database.OrderItemStatusChanges
                                                          .Where(change => kitchenItemIds.Contains(change.OrderItemId))
                                                          .ToListAsync();

      Assert.That(changes,
                  Has.Count.EqualTo(4),
                  "Every kitchen item is logged once when it is placed and once when it is finished.");
    }

    using var openItems = await SendAsync(HttpMethod.Get, "/api/open-items", deviceToken);
    var openBody = JsonDocument.Parse(await openItems.Content.ReadAsStringAsync());
    var table = openBody.RootElement.GetProperty("tables")[0];

    Assert.Multiple(() =>
                    {
                      Assert.That(table.GetProperty("tableName").GetString(), Is.EqualTo("Tisch 3"));
                      Assert.That(table.GetProperty("openAmountCents").GetInt32(), Is.EqualTo(1000));
                      Assert.That(table.GetProperty("items").GetArrayLength(), Is.EqualTo(3));
                    });
  }

  private async Task<string> EnrolAStationTabletAsync(Guid stationId)
  {
    string qrCodeValue;

    using (var invitation = await _factory.Client.PostAsJsonAsync("/api/admin/enrolment/invitations",
                                                                 new { stationId }))
    {
      Assert.That(invitation.StatusCode, Is.EqualTo(HttpStatusCode.Created));
      var body = JsonDocument.Parse(await invitation.Content.ReadAsStringAsync());
      var qrUrl = body.RootElement.GetProperty("qrUrl").GetString()!;
      qrCodeValue = qrUrl[(qrUrl.LastIndexOf('/') + 1)..];
    }

    using var redeemed = await _factory.Client.PostAsJsonAsync("/api/enrolment/redeem",
                                                              new RedeemBody(qrCodeValue, null, "NUnit tablet"));

    Assert.That(redeemed.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var redemption = JsonDocument.Parse(await redeemed.Content.ReadAsStringAsync());

    return redemption.RootElement.GetProperty("deviceToken").GetString()!;
  }

  private async Task<string> EnrolAPhoneAsync()
  {
    string qrCodeValue;

    using (var invitation = await _factory.Client.PostAsJsonAsync("/api/admin/enrolment/invitations",
                                                                 new { staffMemberId = _world.StaffMemberId }))
    {
      Assert.That(invitation.StatusCode, Is.EqualTo(HttpStatusCode.Created));
      var body = JsonDocument.Parse(await invitation.Content.ReadAsStringAsync());
      var qrUrl = body.RootElement.GetProperty("qrUrl").GetString()!;
      qrCodeValue = qrUrl[(qrUrl.LastIndexOf('/') + 1)..];
    }

    using var redeemed = await _factory.Client.PostAsJsonAsync("/api/enrolment/redeem",
                                                              new RedeemBody(qrCodeValue, null, "NUnit"));

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
                           new(selection.BratwurstItemId, selection.BratwurstPriceCents, null, null),
                           new(selection.BratwurstItemId, selection.BratwurstPriceCents, null, null),
                           new(selection.BeerItemId, selection.BeerPriceCents, null, null)
                         ]);

    using var response = await SendAsync(HttpMethod.Post, "/api/orders", deviceToken, body);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var placed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var stationOrders = placed.RootElement.GetProperty("stationOrders");

    return new(placed.RootElement.GetProperty("orderId").GetGuid(),
               placed.RootElement.GetProperty("globalOrderNumber").GetInt32(),
               placed.RootElement.GetProperty("totalCents").GetInt32(),
               stationOrders.GetArrayLength(),
               [.. stationOrders.EnumerateArray().Select(slice => slice.GetProperty("stationOrderNumber").GetInt32())]);
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
  int StationOrderCount,
  IReadOnlyList<int> SequenceNumbers);
