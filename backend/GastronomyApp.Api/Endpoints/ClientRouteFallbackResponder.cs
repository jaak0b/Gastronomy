using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Endpoints;

public sealed class ClientRouteFallbackResponder
{
  private const string ApiPrefix = "/api";
  private const string HubPrefix = "/hub";

  private readonly StationShellResponder _shellResponder;

  public ClientRouteFallbackResponder(StationShellResponder shellResponder)
  {
    _shellResponder = shellResponder;
  }

  public IResult Respond(HttpContext httpContext)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    if (httpContext.Request.Path.StartsWithSegments(ApiPrefix)
        || httpContext.Request.Path.StartsWithSegments(HubPrefix))
    {
      return Results.NotFound();
    }

    return _shellResponder.Respond(httpContext);
  }
}
