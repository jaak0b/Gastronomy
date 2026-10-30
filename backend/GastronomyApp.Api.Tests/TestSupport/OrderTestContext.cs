using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.TestSupport;

public sealed class OrderTestContext : IAsyncDisposable
{
  internal OrderTestContext(ApiTestFactory factory, SeededWorld world, string deviceToken, Guid deviceId)
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
               World.TableId,
               [
                 new(World.BratwurstItemId, 350, null, null),
                 new(World.BratwurstItemId, 350, null, null)
               ]);
  }

  public async Task<Guid> SeedTableAsync(string tableName)
  {
    await using var context = Factory.CreateContext();

    return await new ApiSeeder().SeedTableAsync(context, World.FestivalId, tableName, CancellationToken.None);
  }

  public Task<HttpResponseMessage> PostOrderAsync(OrderBody body)
  {
    return SendAsync(HttpMethod.Post, "/api/orders", body);
  }

  public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null)
  {
    return SendAsAsync(DeviceToken, method, path, body);
  }

  public async Task<HttpResponseMessage> SendAsAsync(string deviceToken, HttpMethod method, string path, object? body = null)
  {
    using HttpRequestMessage request = new(method, path);
    request.Headers.Authorization = new("Bearer", deviceToken);
    if (body is not null)
      request.Content = JsonContent.Create(body, body.GetType());

    return await Client.SendAsync(request);
  }

  public async Task<Guid> FindUnitIdAsync(string name)
  {
    await using var context = Factory.CreateContext();
    var unit = await context.Units.FirstOrDefaultAsync(candidate => candidate.Name == name);

    if (unit is not null)
      return unit.Id;

    unit = new()
           {
             Id = Guid.NewGuid(),
             Name = name
           };
    context.Units.Add(unit);
    await context.SaveChangesAsync();

    return unit.Id;
  }

  public async Task<Guid> FindCategoryIdAsync(string name)
  {
    using var response = await Client.GetAsync("/api/admin/categories");
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    foreach (var category in body.RootElement.GetProperty("categories").EnumerateArray())
      if (category.GetProperty("name").GetString() == name)
        return category.GetProperty("categoryId").GetGuid();

    throw new AssertionException($"The seeded catalog has no category named {name}.");
  }

  public async Task<string> IssueStationTokenAsync(Guid stationId)
  {
    return (await new TestDeviceEnrolment(Factory.Services).EnrolStationAsync(stationId)).PlaintextToken;
  }

  public async Task<string> IssueSecondStaffTokenAsync(string name)
  {
    using var scope = Factory.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AfterCommitActions>().StartCollecting();
    var database = scope.ServiceProvider.GetRequiredService<GastronomyAppDbContext>();
    var staffMemberId = Guid.NewGuid();

    database.StaffMembers.Add(new()
                              {
                                Id = staffMemberId,
                                Name = name,
                                IsActive = true,
                                CreatedAtUtc = DateTime.UtcNow
                              });
    await database.SaveChangesAsync();

    return (await new TestDeviceEnrolment(Factory.Services).EnrolStaffMemberAsync(staffMemberId)).PlaintextToken;
  }
}
