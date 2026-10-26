using GastronomyApp.Api.Responders;
using GastronomyApp.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Responders;

[TestFixture]
public sealed class InvitationQRResponderTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContextBuilder().StartAsync();
    _responder = _context.Factory.Services.GetRequiredService<InvitationQRResponder>();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _context.DisposeAsync();
  }

  private OrderTestContext _context = null!;
  private InvitationQRResponder _responder = null!;

  [Test]
  public void Respond_NullHttpContext_ThrowsArgumentNullException()
  {
    Assert.That(() => _responder.Respond("http://192.168.0.5:5000/j/ABCDEF", null!), Throws.ArgumentNullException);
  }

  [Test]
  public async Task Respond_AnAddress_AnswersWithAPictureThatNobodyMayKeep()
  {
    DefaultHttpContext httpContext = new();
    httpContext.RequestServices = _context.Factory.Services;
    httpContext.Response.Body = new MemoryStream();

    var result = _responder.Respond("http://192.168.0.5:5000/j/ABCDEF", httpContext);
    await result.ExecuteAsync(httpContext);
    httpContext.Response.Body.Position = 0;
    using StreamReader reader = new(httpContext.Response.Body);

    Assert.Multiple(async () =>
                    {
                      Assert.That(httpContext.Response.ContentType, Does.StartWith("image/svg+xml"));
                      Assert.That(httpContext.Response.Headers.CacheControl.ToString(), Does.Contain("no-store"));
                      Assert.That(await reader.ReadToEndAsync(), Does.Contain("<svg"));
                    });
  }
}
