using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Api.Tests.TestSupport;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class OrderPlacementScenarioTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContextBuilder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;

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
                                  Is.EqualTo(new[]
                                             {
                                               1,
                                               1
                                             }),
                                  "Each station keeps its own independent run of sequence numbers.");
                      Assert.That(placed.TotalCents, Is.EqualTo(1000));
                    });
  }

  [Test]
  public async Task OrderPlacementFlow_TwoStationsWithMixedDeliveryModes_ReachesTheTabletsAndTheOpenItemsList()
  {
    var deviceToken = await EnrolAPhoneAsync();
    var kitchenToken = await EnrolAStationTabletAsync(_context.World.KitchenStationId);

    OrderWithDeliveryModesBody order = new(Guid.NewGuid(),
                                           "Tisch 3",
                                           [
                                             new(_context.World.BratwurstItemId, 350, null, null),
                                             new(_context.World.BratwurstItemId, 350, null, null),
                                             new(_context.World.BeerItemId, 300, null, null)
                                           ],
                                           [new(_context.World.BarStationId, "asItComes")]);

    using (var placed = await _context.SendAsAsync(deviceToken, HttpMethod.Post, "/api/orders", order))
    {
      Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    IReadOnlyList<Guid> kitchenItemIds;

    using (var queue = await _context.SendAsAsync(kitchenToken, HttpMethod.Get, "/api/station/orders"))
    {
      Assert.That(queue.StatusCode, Is.EqualTo(HttpStatusCode.OK));

      var body = JsonDocument.Parse(await queue.Content.ReadAsStringAsync());
      var stationOrder = body.RootElement.GetProperty("orders")[0];

      kitchenItemIds = stationOrder.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("orderItemId").GetGuid()).ToList();

      Assert.Multiple(() =>
                      {
                        Assert.That(body.RootElement.GetProperty("orders").GetArrayLength(), Is.EqualTo(1));
                        Assert.That(stationOrder.GetProperty("tableName").GetString(), Is.EqualTo("Tisch 3"));
                        Assert.That(stationOrder.GetProperty("deliveryMode").GetString(), Is.EqualTo("together"));
                        Assert.That(kitchenItemIds, Has.Count.EqualTo(2));
                      });
    }

    using (var fulfilled = await _context.SendAsAsync(kitchenToken, HttpMethod.Post, "/api/station/items/fulfill", new StationItemSelectionBody(kitchenItemIds)))
    {
      Assert.That(fulfilled.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    using var openItems = await _context.SendAsAsync(deviceToken, HttpMethod.Get, "/api/open-items");
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

    using (var invitation = await _context.Client.PostAsJsonAsync("/api/admin/enrolment/invitations", new { stationId }))
    {
      Assert.That(invitation.StatusCode, Is.EqualTo(HttpStatusCode.Created));
      var body = JsonDocument.Parse(await invitation.Content.ReadAsStringAsync());
      var qrUrl = body.RootElement.GetProperty("qrUrl").GetString()!;
      qrCodeValue = qrUrl[(qrUrl.LastIndexOf('/') + 1)..];
    }

    using var redeemed = await _context.Client.PostAsJsonAsync("/api/enrolment/redeem", new RedeemBody(qrCodeValue, null, "NUnit tablet"));

    Assert.That(redeemed.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var redemption = JsonDocument.Parse(await redeemed.Content.ReadAsStringAsync());

    return redemption.RootElement.GetProperty("deviceToken").GetString()!;
  }

  private async Task<string> EnrolAPhoneAsync()
  {
    string qrCodeValue;

    using (var invitation = await _context.Client.PostAsJsonAsync("/api/admin/enrolment/invitations", new { staffMemberId = _context.World.StaffMemberId }))
    {
      Assert.That(invitation.StatusCode, Is.EqualTo(HttpStatusCode.Created));
      var body = JsonDocument.Parse(await invitation.Content.ReadAsStringAsync());
      var qrUrl = body.RootElement.GetProperty("qrUrl").GetString()!;
      qrCodeValue = qrUrl[(qrUrl.LastIndexOf('/') + 1)..];
    }

    using var redeemed = await _context.Client.PostAsJsonAsync("/api/enrolment/redeem", new RedeemBody(qrCodeValue, null, "NUnit"));

    Assert.That(redeemed.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var redemption = JsonDocument.Parse(await redeemed.Content.ReadAsStringAsync());

    return redemption.RootElement.GetProperty("deviceToken").GetString()!;
  }

  private async Task<CatalogSelection> FetchCatalogAsync(string deviceToken)
  {
    using var response = await _context.SendAsAsync(deviceToken, HttpMethod.Get, "/api/catalog");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var items = body.RootElement.GetProperty("items");

    var bratwurst = items.EnumerateArray().First(item => item.GetProperty("id").GetGuid() == _context.World.BratwurstItemId);
    var beer = items.EnumerateArray().First(item => item.GetProperty("id").GetGuid() == _context.World.BeerItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(bratwurst.GetProperty("stationIds").GetArrayLength(), Is.EqualTo(1));
                      Assert.That(beer.GetProperty("stationIds").GetArrayLength(), Is.EqualTo(1));
                    });

    return new(_context.World.BratwurstItemId, bratwurst.GetProperty("priceCents").GetInt32(), _context.World.BeerItemId, beer.GetProperty("priceCents").GetInt32());
  }

  private async Task<PlacedOrder> SendOrderAsync(string deviceToken, CatalogSelection selection)
  {
    OrderBody body = new(Guid.NewGuid(),
                         "Tisch 12",
                         [
                           new(selection.BratwurstItemId, selection.BratwurstPriceCents, null, null),
                           new(selection.BratwurstItemId, selection.BratwurstPriceCents, null, null),
                           new(selection.BeerItemId, selection.BeerPriceCents, null, null)
                         ]);

    using var response = await _context.SendAsAsync(deviceToken, HttpMethod.Post, "/api/orders", body);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    var placed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var stationOrders = placed.RootElement.GetProperty("stationOrders");

    return new(placed.RootElement.GetProperty("orderId").GetGuid(),
               placed.RootElement.GetProperty("globalOrderNumber").GetInt32(),
               placed.RootElement.GetProperty("totalCents").GetInt32(),
               stationOrders.GetArrayLength(),
               stationOrders.EnumerateArray().Select(stationOrder => stationOrder.GetProperty("stationOrderNumber").GetInt32()).ToList());
  }
}
