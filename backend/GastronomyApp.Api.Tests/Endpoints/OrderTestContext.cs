using System.Net.Http.Headers;
using System.Net.Http.Json;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Endpoints;

public sealed record OrderLineBody(Guid CatalogItemId, int Quantity, string? Note, Guid? StationId);

public sealed record OrderBody(
    Guid ClientOrderId,
    string TableLabel,
    string? Note,
    int? ExpectedTotalCents,
    IReadOnlyList<OrderLineBody> Lines);

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

    public HttpClient Client
    {
        get { return Factory.Client; }
    }

    public OrderBody BuildOrder(Guid clientOrderId, int expectedTotalCents)
    {
        return new OrderBody(
            clientOrderId,
            "Tisch 12",
            null,
            expectedTotalCents,
            [new OrderLineBody(World.BratwurstItemId, 2, null, null)]);
    }

    public Task<HttpResponseMessage> PostOrderAsync(OrderBody body)
    {
        return SendAsync(HttpMethod.Post, "/api/orders", body);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null)
    {
        using HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", DeviceToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        return await Client.SendAsync(request);
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
    }

    public sealed class Builder
    {
        public async Task<OrderTestContext> StartAsync(bool withRunningPrinters = true)
        {
            ApiTestFactory factory = await new ApiTestFactory.Builder().StartAsync();
            SeededWorld world;

            await using (GastronomyAppDbContext context = factory.CreateContext())
            {
                world = await new ApiSeeder().SeedAsync(context, CancellationToken.None);
            }

            if (withRunningPrinters)
            {
                await factory.ReconcilePrintersAsync();
            }

            using IServiceScope scope = factory.Services.CreateScope();
            IssuedDeviceToken issued = await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
                .IssueAsync(world.StaffMemberId, "de", "NUnit", CancellationToken.None);

            return new OrderTestContext(factory, world, issued.PlaintextToken, issued.Device.Id);
        }
    }
}
