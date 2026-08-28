using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class LanguageEndpoints
{
  public static IEndpointRouteBuilder MapLanguageEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapGet("/api/language",
                  (AppLanguage language) =>
                    Results.Ok(new LanguageView(language.Current)));

    return routes;
  }
}
