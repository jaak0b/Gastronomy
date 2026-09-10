using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Endpoints;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
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
    app.UseStaticFiles();
    app.UseMiddleware<LoopbackAdminAuthorizationMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapEnrolmentEndpoints();
    app.MapSessionEndpoints();
    app.MapCatalogEndpoints();
    app.MapOrderEndpoints();
    app.MapOpenItemEndpoints();
    app.MapStationEndpoints();
    app.MapHealthEndpoints();
    app.MapLanguageEndpoints();
    app.MapAdminStationEndpoints();
    app.MapAdminCategoryEndpoints();
    app.MapAdminItemEndpoints();
    app.MapAdminStaffMembersEndpoints();
    app.MapAdminEnrolmentEndpoints();
    app.MapAdminOrderEndpoints();
    app.MapAdminInvitationQrEndpoints();
    app.MapAdminFestivalEndpoints();
    app.MapFallback("/{*clientRoute:nonfile}",
                    (HttpContext httpContext, ClientRouteFallbackResponder responder) => responder.Respond(httpContext));
    app.MapHub<GastronomyHub>("/hub");
  }
}
