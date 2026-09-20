using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Endpoints;

public sealed class StationShellResponder
{
  private readonly IWebHostEnvironment _environment;

  public StationShellResponder(IWebHostEnvironment environment)
  {
    _environment = environment;
  }

  public IResult Respond(HttpContext httpContext)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    var shellPath = Path.Combine(_environment.WebRootPath ?? string.Empty, "index.html");

    if (!File.Exists(shellPath))
      return Results.NotFound();

    httpContext.Response.Headers.CacheControl = "no-cache";

    return Results.File(shellPath, "text/html");
  }
}
