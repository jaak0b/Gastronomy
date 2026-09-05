using System.Net.Http.Json;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Endpoints;

public sealed record OrderItemBody(Guid CatalogItemId, int UnitPriceCents, string? Note, Guid? StationId);

public sealed record SlipOnThePileBody(bool SlipIsOnThePile);

public sealed record OrderBody(
  Guid ClientOrderId,
  string TableName,
  string? Note,
  IReadOnlyList<OrderItemBody> Items,
  bool SettleOnSend = false);

public sealed record SettleItemsBody(IReadOnlyList<Guid> OrderItemIds);

public sealed record SettleFreeOfChargeBody(IReadOnlyList<Guid> OrderItemIds, string? PaymentNotice);

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

  public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null)
  {
    using HttpRequestMessage request = new(method, path);
    request.Headers.Authorization = new("Bearer", DeviceToken);
    if (body is not null)
    {
      request.Content = JsonContent.Create(body, body.GetType());
    }

    return await Client.SendAsync(request);
  }

  public sealed class Builder
  {
    public async Task<OrderTestContext> StartAsync(bool withRunningPrinters = true)
    {
      var factory = await new ApiTestFactory.Builder().StartAsync();
      SeededWorld world;

      await using (var context = factory.CreateContext())
      {
        world = await new ApiSeeder().SeedAsync(context, CancellationToken.None);
      }

      if (withRunningPrinters)
      {
        await factory.ReconcilePrintersAsync();
      }

      using var scope = factory.Services.CreateScope();
      var issued = await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
                              .IssueAsync(world.StaffMemberId, "de", "NUnit", CancellationToken.None);

      return new(factory, world, issued.PlaintextToken, issued.Device.Id);
    }
  }
}
