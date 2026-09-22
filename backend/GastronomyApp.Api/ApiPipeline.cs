using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Endpoints;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Filters;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Responders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api;

public sealed class ApiPipeline
{
  public void Configure(WebApplication app)
  {
    ArgumentNullException.ThrowIfNull(app);

    app.UseMiddleware<InfrastructureExceptionMiddleware>();
    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions
    {
      OnPrepareResponse = staticFileResponse =>
                          {
                            if (staticFileResponse.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                              staticFileResponse.Context.Response.Headers.CacheControl = "no-cache";
                          }
    });
    app.UseMiddleware<LoopbackAdminAuthorizationMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    var routesInOneTransaction = app.MapGroup(string.Empty).AddEndpointFilter<RequestTransactionFilter>();

    routesInOneTransaction.MapEnrolmentEndpoints();
    routesInOneTransaction.MapSessionEndpoints();
    routesInOneTransaction.MapCatalogEndpoints();
    routesInOneTransaction.MapOrderEndpoints();
    routesInOneTransaction.MapOpenItemEndpoints();
    routesInOneTransaction.MapStationEndpoints();
    routesInOneTransaction.MapLanguageEndpoints();
    routesInOneTransaction.MapAdminStationEndpoints();
    routesInOneTransaction.MapAdminCategoryEndpoints();
    routesInOneTransaction.MapAdminItemEndpoints();
    routesInOneTransaction.MapAdminStaffMembersEndpoints();
    routesInOneTransaction.MapAdminEnrolmentEndpoints();
    routesInOneTransaction.MapAdminInvitationQREndpoints();
    routesInOneTransaction.MapAdminFestivalEndpoints();
    app.MapFallback("/{*clientRoute:nonfile}", (HttpContext httpContext, ClientRouteFallbackResponder responder) => responder.Respond(httpContext));
    app.MapHub<GastronomyHub>("/hub");
  }
}
