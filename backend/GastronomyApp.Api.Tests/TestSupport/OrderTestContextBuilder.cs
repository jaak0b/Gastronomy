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

    var enrolled = await new TestDeviceEnrolment(factory.Services).EnrolStaffMemberAsync(world.StaffMemberId);

    return new(factory, world, enrolled.PlaintextToken, enrolled.DeviceId);
  }
}
