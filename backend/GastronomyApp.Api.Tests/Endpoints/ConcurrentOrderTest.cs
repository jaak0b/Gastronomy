using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class ConcurrentOrderTest
{
    private OrderTestContext context = null!;
    private string secondDeviceToken = null!;

    [SetUp]
    public async Task SetUp()
    {
        context = await new OrderTestContext.Builder().StartAsync(withRunningPrinters: false);

        using IServiceScope scope = context.Factory.Services.CreateScope();
        IssuedDeviceToken issued = await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
            .IssueAsync(context.World.ServerPersonId, "de", "NUnit second phone", CancellationToken.None);
        secondDeviceToken = issued.PlaintextToken;
    }

    [TearDown]
    public async Task TearDown()
    {
        await context.DisposeAsync();
    }

    [Test]
    public async Task PostOrder_TwoServersSendingAtTheSameMoment_AcceptsBothWithDistinctNumbers()
    {
        Task<HttpResponseMessage> first = SendOrderAsync(context.DeviceToken);
        Task<HttpResponseMessage> second = SendOrderAsync(secondDeviceToken);

        HttpResponseMessage[] responses = await Task.WhenAll(first, second);
        IReadOnlyList<HttpStatusCode> statuses = [.. responses.Select(response => response.StatusCode)];

        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }

        await using GastronomyAppDbContext database = context.Factory.CreateContext();
        List<int> orderNumbers = await database.Orders
            .Select(order => order.GlobalOrderNumber)
            .OrderBy(number => number)
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                statuses,
                Is.All.EqualTo(HttpStatusCode.Created),
                "Two servers sending at the same moment must both be accepted.");
            Assert.That(orderNumbers, Is.EqualTo(new[] { 1, 2 }), "Each order keeps its own global number.");
        });
    }

    private Task<HttpResponseMessage> SendOrderAsync(string deviceToken)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);
        request.Content = JsonContent.Create(context.BuildOrder(Guid.NewGuid(), 700));

        return context.Client.SendAsync(request);
    }
}
