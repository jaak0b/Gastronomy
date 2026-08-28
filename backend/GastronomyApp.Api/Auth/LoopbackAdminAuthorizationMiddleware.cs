using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Auth;

public sealed class LocalAddressSet
{
  public bool Contains(IPAddress? address)
  {
    if (address is null)
    {
      return false;
    }

    if (IPAddress.IsLoopback(address))
    {
      return true;
    }

    IPAddress candidate = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
    {
      foreach (UnicastIPAddressInformation unicast in networkInterface.GetIPProperties().UnicastAddresses)
      {
        if (unicast.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6
            && unicast.Address.Equals(candidate))
        {
          return true;
        }
      }
    }

    return false;
  }
}

public sealed class LoopbackAdminAuthorizationMiddleware : IMiddleware
{
  private const string AdminApiPrefix = "/api/admin";

  private readonly LocalAddressSet localAddresses;

  public LoopbackAdminAuthorizationMiddleware(LocalAddressSet localAddresses)
  {
    this.localAddresses = localAddresses;
  }

  public async Task InvokeAsync(HttpContext context, RequestDelegate next)
  {
    if (!context.Request.Path.StartsWithSegments(AdminApiPrefix))
    {
      await next(context);
      return;
    }

    if (localAddresses.Contains(context.Connection.RemoteIpAddress))
    {
      await next(context);
      return;
    }

    context.Response.StatusCode = StatusCodes.Status404NotFound;
  }
}
