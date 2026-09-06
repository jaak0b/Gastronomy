using GastronomyApp.Api.Tests.Endpoints;
using Microsoft.AspNetCore.SignalR.Client;

namespace GastronomyApp.Api.Tests.Hub;

[TestFixture]
public sealed class HubGroupMembershipTest
{

  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;

  [Test]
  public async Task Connect_ValidDeviceTokenAsAccessTokenQuery_IsAccepted()
  {
    await using var connection = Connect(_context.DeviceToken);

    await connection.StartAsync();

    Assert.That(connection.State, Is.EqualTo(HubConnectionState.Connected));
  }

  private HubConnection Connect(string deviceToken)
  {
    return new HubConnectionBuilder()
          .WithUrl(new Uri(_context.Factory.BaseAddress, $"hub?access_token={deviceToken}"))
          .Build();
  }
}

