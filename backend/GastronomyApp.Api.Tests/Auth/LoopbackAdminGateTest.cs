using System.Net;
using GastronomyApp.Api.Auth;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Tests.Auth;

[TestFixture]
public sealed class LoopbackAdminGateTest
{
  private readonly LoopbackAdminAuthorizationMiddleware middleware = new(new LocalAddressSet());

  [TestCase("127.0.0.1")]
  [TestCase("::1")]
  public async Task InvokeAsync_AdminPathFromTheLaptopItself_ReachesTheEndpoint(string remoteAddress)
  {
    DefaultHttpContext context = ContextFor("/api/admin/stations", remoteAddress);
    bool reachedTheEndpoint = false;

    await middleware.InvokeAsync(
        context,
        _ =>
        {
          reachedTheEndpoint = true;
          return Task.CompletedTask;
        });

    Assert.That(reachedTheEndpoint, Is.True);
  }

  [Test]
  public async Task InvokeAsync_AdminPathFromAnyOtherAddress_AnswersNotFound()
  {
    DefaultHttpContext context = ContextFor("/api/admin/stations", "203.0.113.9");
    bool reachedTheEndpoint = false;

    await middleware.InvokeAsync(
        context,
        _ =>
        {
          reachedTheEndpoint = true;
          return Task.CompletedTask;
        });

    Assert.Multiple(() =>
    {
      Assert.That(reachedTheEndpoint, Is.False);
      Assert.That(context.Response.StatusCode, Is.EqualTo(404));
    });
  }

  [Test]
  public async Task InvokeAsync_ServedAdminPageFromAnyAddress_ReachesTheEndpoint()
  {
    DefaultHttpContext context = ContextFor("/admin", "203.0.113.9");
    bool reachedTheEndpoint = false;

    await middleware.InvokeAsync(
        context,
        _ =>
        {
          reachedTheEndpoint = true;
          return Task.CompletedTask;
        });

    Assert.That(reachedTheEndpoint, Is.True);
  }

  private DefaultHttpContext ContextFor(string path, string remoteAddress)
  {
    DefaultHttpContext context = new();
    context.Request.Path = path;
    context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
    return context;
  }
}
