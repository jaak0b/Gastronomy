using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Auth;

public sealed class LoopbackAdminAuthorizationMiddleware : IMiddleware
{
  private const string AdminApiPrefix = "/api/admin";

  private readonly LocalAddressSet _localAddresses;

  public LoopbackAdminAuthorizationMiddleware(LocalAddressSet localAddresses)
  {
    _localAddresses = localAddresses;
  }

  public async Task InvokeAsync(HttpContext context, RequestDelegate next)
  {
    if (!context.Request.Path.StartsWithSegments(AdminApiPrefix))
    {
      await next(context);
      return;
    }

    if (_localAddresses.Contains(context.Connection.RemoteIpAddress))
    {
      await next(context);
      return;
    }

    context.Response.StatusCode = StatusCodes.Status404NotFound;
  }
}
