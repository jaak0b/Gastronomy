using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class ResolveRaceTest
{

  [SetUp]
  public async Task SetUp()
  {
    context = await new OrderTestContext.Builder().StartAsync(false);

    using var created = await context.PostOrderAsync(context.BuildOrder(Guid.NewGuid()));
    var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
    orderId = body.RootElement.GetProperty("orderId").GetGuid();
    ticketId = body.RootElement.GetProperty("stationOrders")[0].GetProperty("stationOrderId").GetGuid();

    await using var database = context.Factory.CreateContext();
    await database.PrintJobs
                  .Where(job => job.StationOrderId == ticketId)
                  .ExecuteUpdateAsync(job => job.SetProperty(entry => entry.Status, PrintJobStatus.Unknown));
    await database.SaveChangesAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await context.DisposeAsync();
  }

  private OrderTestContext context = null!;
  private Guid orderId;
  private Guid ticketId;

  [Test]
  public async Task PostResolve_TwoSimultaneousAnswers_AcceptsOneAndNeverReprintsTwice()
  {
    Task<HttpResponseMessage> first = ResolveAsync();
    Task<HttpResponseMessage> second = ResolveAsync();

    HttpResponseMessage[] responses = await Task.WhenAll(first, second);

    var accepted = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
    var refused = responses.Count(response => response.StatusCode == HttpStatusCode.Conflict);

    foreach (var response in responses)
    {
      response.Dispose();
    }

    await using var database = context.Factory.CreateContext();
    var printJobs = await database.PrintJobs.CountAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(accepted, Is.EqualTo(1), "Exactly one answer to the question may win.");
                      Assert.That(refused, Is.EqualTo(1), "The losing answer must be told the question was already answered.");
                      Assert.That(printJobs, Is.EqualTo(1), "One question may never leave two slips waiting to print.");
                    });
  }

  private Task<HttpResponseMessage> ResolveAsync()
  {
    return context.Client.SendAsync(BuildRequest());
  }

  private HttpRequestMessage BuildRequest()
  {
    HttpRequestMessage request = new(HttpMethod.Post,
                                     $"/api/orders/{orderId}/station-orders/{ticketId}/resolve");
    request.Headers.Authorization =
      new("Bearer", context.DeviceToken);
    request.Content = JsonContent.Create(new SlipOnThePileBody(false));

    return request;
  }
}
