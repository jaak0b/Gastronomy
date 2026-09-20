using GastronomyApp.Api.Endpoints;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class InvitationQRRendererTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContextBuilder().StartAsync();
    _scope = _context.Factory.Services.CreateScope();
  }

  [TearDown]
  public async Task TearDown()
  {
    _scope.Dispose();
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;
  private IServiceScope _scope = null!;

  [Test]
  public void RenderAsync_NullHttpContext_ThrowsArgumentNullException()
  {
    var renderer = _scope.ServiceProvider.GetRequiredService<InvitationQRRenderer>();

    Assert.That(async () => await renderer.RenderAsync(Guid.NewGuid(), null!, CancellationToken.None), Throws.ArgumentNullException);
  }
}
