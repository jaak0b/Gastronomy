using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.TestSupport;

public sealed class OrderTestContextBuilder
{
  public async Task<OrderTestContext> StartAsync()
  {
    var factory = await new ApiTestFactoryBuilder().StartAsync();
    SeededWorld world;

    await using (var context = factory.CreateContext())
    {
      world = await new ApiSeeder().SeedAsync(context, CancellationToken.None);
    }

    using var scope = factory.Services.CreateScope();
    var owner = await scope.ServiceProvider.GetRequiredService<IDeviceOwnerStore>().FindAsync(DeviceOwnerKind.StaffMember, world.StaffMemberId, CancellationToken.None);
    var issued = await scope.ServiceProvider.GetRequiredService<IDeviceTokenStore>().IssueAsync(owner!, "de", "NUnit", CancellationToken.None);

    return new(factory, world, issued.PlaintextToken, issued.Device.Id);
  }
}
