using System.Net;
using System.Net.Http.Json;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class ConcurrentOrderTest
{

  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync(false);

    using var scope = _context.Factory.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<GastronomyAppDbContext>();

    var secondStaffMemberId = Guid.NewGuid();
    database.StaffMembers.Add(new()
                              {
                                Id = secondStaffMemberId,
                                Name = "Bernd",
                                IsActive = true,
                                CreatedAtUtc = DateTime.UtcNow
                              });
    await database.SaveChangesAsync();

    var issued = await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>()
                            .IssueAsync(secondStaffMemberId, "de", "NUnit second phone", CancellationToken.None);
    _secondDeviceToken = issued.PlaintextToken;
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;
  private string _secondDeviceToken = null!;

  [Test]
  public async Task PostOrder_TwoServersSendingAtTheSameMoment_AcceptsBothWithDistinctNumbers()
  {
    Task<HttpResponseMessage> first = SendOrderAsync(_context.DeviceToken);
    Task<HttpResponseMessage> second = SendOrderAsync(_secondDeviceToken);

    HttpResponseMessage[] responses = await Task.WhenAll(first, second);
    IReadOnlyList<HttpStatusCode> statuses = [.. responses.Select(response => response.StatusCode)];

    foreach (var response in responses)
    {
      response.Dispose();
    }

    await using var database = _context.Factory.CreateContext();
    List<int> orderNumbers = await database.Orders
                                           .Select(order => order.GlobalOrderNumber)
                                           .OrderBy(number => number)
                                           .ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(statuses,
                                  Is.All.EqualTo(HttpStatusCode.Created),
                                  "Two servers sending at the same moment must both be accepted.");
                      Assert.That(orderNumbers, Is.EqualTo(new[] { 1, 2 }), "Each order keeps its own global number.");
                    });
  }

  private Task<HttpResponseMessage> SendOrderAsync(string deviceToken)
  {
    HttpRequestMessage request = new(HttpMethod.Post, "/api/orders");
    request.Headers.Authorization = new("Bearer", deviceToken);
    request.Content = JsonContent.Create(_context.BuildOrder(Guid.NewGuid()));

    return _context.Client.SendAsync(request);
  }
}
