using GastronomyApp.Api.Handlers;
using GastronomyApp.Contracts.Admin.Ingredients;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminIngredientEndpoints
{
  public static IEndpointRouteBuilder MapAdminIngredientEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/ingredients");

    group.MapGet(string.Empty, async (AdminIngredientHandler handler, CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    group.MapPost(string.Empty, async (SaveIngredientRequest request, AdminIngredientHandler handler, CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken));

    group.MapPut("/{ingredientId:guid}", async (Guid ingredientId, SaveIngredientRequest request, AdminIngredientHandler handler, CancellationToken cancellationToken) => await handler.UpdateAsync(ingredientId, request, cancellationToken));

    group.MapPost("/{ingredientId:guid}/activate", async (Guid ingredientId, AdminIngredientHandler handler, CancellationToken cancellationToken) => await handler.ActivateAsync(ingredientId, cancellationToken));

    group.MapPost("/{ingredientId:guid}/deactivate", async (Guid ingredientId, AdminIngredientHandler handler, CancellationToken cancellationToken) => await handler.DeactivateAsync(ingredientId, cancellationToken));

    return routes;
  }
}
