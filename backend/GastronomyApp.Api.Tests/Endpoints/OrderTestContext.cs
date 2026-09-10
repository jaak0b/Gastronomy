using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Endpoints;

public sealed record OrderItemBody(Guid CatalogItemId, int UnitPriceCents, string? Note, Guid? StationId);

public sealed record OrderBody(
  Guid ClientOrderId,
  string TableName,
  string? Note,
  IReadOnlyList<OrderItemBody> Items,
  bool SettleOnSend = false);

public sealed record SettleItemsBody(
  IReadOnlyList<Guid> OrderItemIds,
  int? AmountPaidCents,
  string? PaymentNotice = null);

public sealed class OrderTestContext : IAsyncDisposable
{
  private OrderTestContext(ApiTestFactory factory, SeededWorld world, string deviceToken, Guid deviceId)
  {
    Factory = factory;
    World = world;
    DeviceToken = deviceToken;
    DeviceId = deviceId;
  }

  public ApiTestFactory Factory { get; }

  public SeededWorld World { get; }

  public string DeviceToken { get; }

  public Guid DeviceId { get; }

  public HttpClient Client => Factory.Client;

  public async ValueTask DisposeAsync()
  {
    await Factory.DisposeAsync();
  }

  public OrderBody BuildOrder(Guid clientOrderId)
  {
    return new(clientOrderId,
               "Tisch 12",
               null,
               [new(World.BratwurstItemId, 350, null, null), new(World.BratwurstItemId, 350, null, null)]);
  }

  public Task<HttpResponseMessage> PostOrderAsync(OrderBody body)
  {
    return SendAsync(HttpMethod.Post, "/api/orders", body);
  }

  public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null)
  {
    return SendAsAsync(DeviceToken, method, path, body);
  }

  public async Task<HttpResponseMessage> SendAsAsync(string deviceToken,
                                                     HttpMethod method,
                                                     string path,
                                                     object? body = null)
  {
    using HttpRequestMessage request = new(method, path);
    request.Headers.Authorization = new("Bearer", deviceToken);
    if (body is not null)
    {
      request.Content = JsonContent.Create(body, body.GetType());
    }

    return await Client.SendAsync(request);
  }

  public async Task<Guid> CategoryIdOfAsync(string name)
  {
    using var response = await Client.GetAsync("/api/admin/categories");
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    foreach (var category in body.RootElement.GetProperty("categories").EnumerateArray())
    {
      if (category.GetProperty("name").GetString() == name)
      {
        return category.GetProperty("categoryId").GetGuid();
      }
    }

    throw new AssertionException($"The seeded catalog has no category named {name}.");
  }

  public async Task<string> IssueStationTokenAsync(Guid stationId)
  {
    using var scope = Factory.Services.CreateScope();
    var issued = await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
                            .IssueAsync(new(DeviceOwnerKind.Station, stationId),
                                        "de",
                                        "NUnit tablet",
                                        CancellationToken.None);

    return issued.PlaintextToken;
  }

  public sealed class Builder
  {
    public async Task<OrderTestContext> StartAsync()
    {
      var factory = await new ApiTestFactory.Builder().StartAsync();
      SeededWorld world;

      await using (var context = factory.CreateContext())
      {
        world = await new ApiSeeder().SeedAsync(context, CancellationToken.None);
      }

      using var scope = factory.Services.CreateScope();
      var issued = await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
                              .IssueAsync(new(DeviceOwnerKind.StaffMember, world.StaffMemberId),
                                          "de",
                                          "NUnit",
                                          CancellationToken.None);

      return new(factory, world, issued.PlaintextToken, issued.Device.Id);
    }
  }
}
